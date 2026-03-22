using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;

namespace VNEditor.Services;

public enum GitPullKind
{
    NotRepository,
    NoUpstream,
    UpToDate,
    FastForward,
    MergedResolvedRemote,
    Error
}

public sealed class GitPullMergeResult
{
    public bool Success { get; init; }
    public GitPullKind Kind { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<(string SceneName, string IdPart)> ConflictLineWarnings { get; init; } =
        Array.Empty<(string, string)>();
}

public static class ProjectGitService
{
    public static string? GetPrimaryRemoteUrl(string workDir)
    {
        var root = Repository.Discover(workDir);
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        using var repo = new Repository(root);
        var name = repo.Head.RemoteName;
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return repo.Network.Remotes[name]?.Url;
    }

    public static bool IsHttpsRemote(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGitRepository(string workDir)
    {
        return !string.IsNullOrEmpty(Repository.Discover(workDir));
    }

    public static int GetAheadBy(string workDir)
    {
        var root = Repository.Discover(workDir);
        if (string.IsNullOrEmpty(root))
        {
            return 0;
        }

        using var repo = new Repository(root);
        return repo.Head.TrackingDetails?.AheadBy ?? 0;
    }

    /// <summary>打开工程等：拉取并与远端合并；若有冲突则按远端版本自动解决。</summary>
    public static GitPullMergeResult PullMergePreferRemote(string workDir, GitHubCredentials? credentials = null) =>
        PullMergeCore(workDir, credentials, abortOnMergeConflict: false);

    /// <summary>拉取合并（冲突按远端）后执行 <c>git lfs pull</c>，拉取 Git LFS 大文件内容。</summary>
    public static GitPullMergeResult PullMergePreferRemoteWithLfs(string workDir, GitHubCredentials? credentials = null)
    {
        var merge = PullMergePreferRemote(workDir, credentials);
        if (!merge.Success)
        {
            return merge;
        }

        var (lfsOk, lfsErr) = TryRunGitLfsPull(workDir);
        if (!lfsOk)
        {
            return new GitPullMergeResult
            {
                Success = false,
                Kind = GitPullKind.Error,
                ErrorMessage = lfsErr ?? "git lfs pull 失败。"
            };
        }

        return merge;
    }

    /// <summary>在仓库根目录执行 <c>git lfs pull</c> 与 <c>git lfs checkout</c>（将指针替换为真实内容）。优先使用程序目录下的 <c>git-lfs.exe</c>。</summary>
    public static (bool Ok, string? Error) TryRunGitLfsPull(string workDir)
    {
        var root = Repository.Discover(workDir);
        if (string.IsNullOrEmpty(root))
        {
            return (false, "未找到 Git 仓库。");
        }

        var pull = RunGitLfsSubcommand(root, "pull", "lfs pull");
        if (!pull.Ok)
        {
            return (false, "git lfs pull 失败：\n" + pull.Error);
        }

        // LibGit2Sharp 签出等写入的是指针文本时，pull 下载对象后需 checkout 才能覆盖工作区
        var checkout = RunGitLfsSubcommand(root, "checkout", "lfs checkout");
        if (!checkout.Ok)
        {
            return (false, "git lfs checkout 失败：\n" + checkout.Error);
        }

        return (true, null);
    }

    private static (bool Ok, string? Error) RunGitLfsSubcommand(
        string repoRoot,
        string bundledArguments,
        string gitCliArguments)
    {
        var bundled = FindBundledGitLfsExecutable();
        string fileName;
        string arguments;
        if (bundled != null)
        {
            fileName = bundled;
            arguments = bundledArguments;
        }
        else
        {
            fileName = "git";
            arguments = gitCliArguments;
        }

        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.Start();
            var stderr = p.StandardError.ReadToEnd();
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(300_000);
            if (p.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                detail = string.IsNullOrWhiteSpace(detail) ? $"退出码 {p.ExitCode}" : detail.Trim();
                return (false, detail);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            var hint = bundled != null
                ? $"无法执行：{bundled}"
                : "请确认已安装 Git 与 Git LFS，或将 git-lfs.exe 放在程序目录，且 git 在 PATH 中";
            return (false, hint + "\n" + ex.Message);
        }
    }

    /// <summary>与主程序同目录的便携 Git LFS（单文件发布时也在 exe 旁）。</summary>
    private static string? FindBundledGitLfsExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(baseDir))
        {
            return null;
        }

        var win = Path.Combine(baseDir, "git-lfs.exe");
        if (File.Exists(win))
        {
            return win;
        }

        var unix = Path.Combine(baseDir, "git-lfs");
        if (File.Exists(unix))
        {
            return unix;
        }

        return null;
    }

    /// <summary>上传前：先拉取合并；若存在合并冲突则中止（恢复合并前状态），不允许上传。</summary>
    public static GitPullMergeResult PullMergeBeforePush(string workDir, GitHubCredentials? credentials = null) =>
        PullMergeCore(workDir, credentials, abortOnMergeConflict: true);

    private static GitPullMergeResult PullMergeCore(string workDir, GitHubCredentials? credentials, bool abortOnMergeConflict)
    {
        var root = Repository.Discover(workDir);
        if (string.IsNullOrEmpty(root))
        {
            return new GitPullMergeResult { Success = false, Kind = GitPullKind.NotRepository, ErrorMessage = "未找到 Git 仓库。" };
        }

        using var repo = new Repository(root);
        var sig = BuildSignature(repo);
        var head = repo.Head;
        if (!head.IsTracking || head.TrackedBranch == null)
        {
            return new GitPullMergeResult { Success = true, Kind = GitPullKind.NoUpstream };
        }

        var remoteName = head.RemoteName;
        if (string.IsNullOrEmpty(remoteName))
        {
            return new GitPullMergeResult { Success = true, Kind = GitPullKind.NoUpstream };
        }

        try
        {
            var fetchOpts = CreateFetchOptions(credentials);
            Commands.Fetch(repo, remoteName, Array.Empty<string>(), fetchOpts, null);
        }
        catch (Exception ex)
        {
            return new GitPullMergeResult
            {
                Success = false,
                Kind = GitPullKind.Error,
                ErrorMessage = FormatGitHttpError(ex, workDir)
            };
        }

        var upstreamTip = head.TrackedBranch.Tip;
        if (upstreamTip == null)
        {
            return new GitPullMergeResult { Success = false, Kind = GitPullKind.Error, ErrorMessage = "无法读取上游分支。" };
        }

        if (upstreamTip.Id.Equals(head.Tip.Id))
        {
            return new GitPullMergeResult { Success = true, Kind = GitPullKind.UpToDate };
        }

        var preTip = head.Tip;
        MergeResult mergeResult;
        try
        {
            mergeResult = repo.Merge(upstreamTip, sig, new MergeOptions());
        }
        catch (Exception ex)
        {
            return new GitPullMergeResult { Success = false, Kind = GitPullKind.Error, ErrorMessage = ex.Message };
        }

        switch (mergeResult.Status)
        {
            case MergeStatus.UpToDate:
                return new GitPullMergeResult { Success = true, Kind = GitPullKind.UpToDate };
            case MergeStatus.FastForward:
                return new GitPullMergeResult { Success = true, Kind = GitPullKind.FastForward };
            case MergeStatus.Conflicts:
                if (abortOnMergeConflict)
                {
                    try
                    {
                        repo.Reset(ResetMode.Hard, preTip);
                    }
                    catch (Exception ex)
                    {
                        return new GitPullMergeResult
                        {
                            Success = false,
                            Kind = GitPullKind.Error,
                            ErrorMessage = "与远端合并发生冲突，且无法恢复本地状态：" + ex.Message
                        };
                    }

                    return new GitPullMergeResult
                    {
                        Success = false,
                        Kind = GitPullKind.Error,
                        ErrorMessage = "与远端存在合并冲突，无法上传。请先在本机解决冲突或通过签出对齐后再上传。"
                    };
                }

                return ResolveConflictsPreferTheirs(repo, sig);
            default:
                return new GitPullMergeResult { Success = true, Kind = GitPullKind.FastForward };
        }
    }

    private static GitPullMergeResult ResolveConflictsPreferTheirs(Repository repo, Signature sig)
    {
        var warnings = new HashSet<(string SceneName, string IdPart)>();
        var utf8 = new UTF8Encoding(false);

        foreach (var conflict in repo.Index.Conflicts)
        {
            var path = conflict.Ours?.Path ?? conflict.Theirs?.Path;
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            var full = Path.GetFullPath(Path.Combine(repo.Info.WorkingDirectory, path));
            if (File.Exists(full))
            {
                var txt = File.ReadAllText(full, utf8);
                foreach (var w in GitConflictCsvParser.CollectLineWarnings(path, txt))
                {
                    warnings.Add(w);
                }
            }

            if (conflict.Theirs != null)
            {
                var blob = repo.Lookup<Blob>(conflict.Theirs.Id);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, blob.GetContentText(utf8), utf8);
                Commands.Stage(repo, path);
            }
            else if (conflict.Ours != null)
            {
                // 远端删除：按远端删除
                if (File.Exists(full))
                {
                    File.Delete(full);
                }

                Commands.Stage(repo, path);
            }
        }

        try
        {
            repo.Commit("Merge: 冲突按远端版本解决 (VNEditor)", sig, sig, new CommitOptions());
        }
        catch (Exception ex)
        {
            return new GitPullMergeResult { Success = false, Kind = GitPullKind.Error, ErrorMessage = ex.Message };
        }

        return new GitPullMergeResult
        {
            Success = true,
            Kind = GitPullKind.MergedResolvedRemote,
            ConflictLineWarnings = warnings.ToList()
        };
    }

    public static (bool Ok, string? Error) CommitTrackedFiles(string workDir, IReadOnlyList<string> absoluteFilePaths, string message)
    {
        var root = Repository.Discover(workDir);
        if (string.IsNullOrEmpty(root))
        {
            return (false, "未找到 Git 仓库。");
        }

        using var repo = new Repository(root);
        var sig = BuildSignature(repo);
        var workRoot = Path.GetFullPath(repo.Info.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        foreach (var abs in absoluteFilePaths)
        {
            if (string.IsNullOrWhiteSpace(abs) || !File.Exists(abs))
            {
                continue;
            }

            var rel = GetRepoRelativePath(workRoot, abs);
            if (rel == null)
            {
                continue;
            }

            Commands.Stage(repo, rel);
        }

        if (!HasStagedChanges(repo))
        {
            return (true, null);
        }

        try
        {
            repo.Commit(message, sig, sig, new CommitOptions());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        return (true, null);
    }

    public static (bool Ok, string? Error) Push(string workDir, GitHubCredentials? credentials = null)
    {
        var root = Repository.Discover(workDir);
        if (string.IsNullOrEmpty(root))
        {
            return (false, "未找到 Git 仓库。");
        }

        using var repo = new Repository(root);
        var head = repo.Head;
        if (!head.IsTracking || head.TrackedBranch == null)
        {
            return (false, "当前分支未设置上游，无法推送。");
        }

        try
        {
            var pushOpts = CreatePushOptions(credentials);
            repo.Network.Push(head, pushOpts);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        return (true, null);
    }

    /// <summary>从远端跟踪分支检出指定工作区文件并覆盖本地。</summary>
    public static (bool Ok, string? Error) CheckoutFilesFromTrackedRemote(
        string workDir,
        IReadOnlyList<string> absoluteFilePaths,
        GitHubCredentials? credentials = null)
    {
        var root = Repository.Discover(workDir);
        if (string.IsNullOrEmpty(root))
        {
            return (false, "未找到 Git 仓库。");
        }

        using var repo = new Repository(root);
        var head = repo.Head;
        if (!head.IsTracking || head.TrackedBranch == null)
        {
            return (false, "当前分支未设置上游，无法从远端拉取单文件。");
        }

        try
        {
            var fetchOpts = CreateFetchOptions(credentials);
            Commands.Fetch(repo, head.RemoteName, Array.Empty<string>(), fetchOpts, null);
        }
        catch (Exception ex)
        {
            return (false, FormatGitHttpError(ex, workDir));
        }

        var tip = head.TrackedBranch.Tip;
        if (tip == null)
        {
            return (false, "无法读取远端提交。");
        }

        var workRoot = Path.GetFullPath(repo.Info.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var utf8 = new UTF8Encoding(false);

        foreach (var abs in absoluteFilePaths)
        {
            if (string.IsNullOrWhiteSpace(abs))
            {
                continue;
            }

            var rel = GetRepoRelativePath(workRoot, abs);
            if (rel == null)
            {
                continue;
            }

            var treePath = rel.Replace('\\', '/');
            var entry = tip[treePath];
            if (entry == null)
            {
                continue;
            }

            if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                var blob = (Blob)entry.Target;
                Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
                File.WriteAllText(abs, blob.GetContentText(utf8), utf8);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 从远端跟踪分支检出当前工程下 <c>DataConfigs/Data/RoleData</c> 与 <c>DataConfigs/Text/RoleData</c> 中的全部 CSV。
    /// 路径按<strong>远端提交树</strong>枚举，不依赖本地是否已有文件；并校验「工程根」落在当前 Git 工作区内。
    /// </summary>
    public static (bool Ok, string? Error) TryCheckoutRoleDataFromTrackedRemote(
        string workDir,
        string projectRoot,
        GitHubCredentials? credentials = null)
    {
        var root = Repository.Discover(workDir);
        if (string.IsNullOrEmpty(root))
        {
            return (false, "未找到 Git 仓库。");
        }

        using var repo = new Repository(root);
        var head = repo.Head;
        if (!head.IsTracking || head.TrackedBranch == null)
        {
            return (false, "当前分支未设置上游，无法从远端拉取角色文件。");
        }

        try
        {
            var fetchOpts = CreateFetchOptions(credentials);
            Commands.Fetch(repo, head.RemoteName, Array.Empty<string>(), fetchOpts, null);
        }
        catch (Exception ex)
        {
            return (false, FormatGitHttpError(ex, workDir));
        }

        var tip = head.TrackedBranch.Tip;
        if (tip == null)
        {
            return (false, "无法读取远端提交。");
        }

        var workRoot = Path.GetFullPath(repo.Info.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var projAbs = Path.GetFullPath(projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var projRel = GetRepoRelativePath(workRoot, projAbs);
        if (projRel == null)
        {
            return (false,
                "当前打开的「工程根」不在该 Git 仓库的工作目录内，无法对齐远端路径。\n"
                + $"工程根：{projAbs}\n仓库根：{workRoot}\n"
                + "请用 VNEditor 打开包含 DataConfigs 的工程根（与 clone 下来的结构一致）。");
        }

        var projRelNorm = projRel.Replace('\\', '/').Trim('/');
        if (projRelNorm == ".")
        {
            projRelNorm = string.Empty;
        }

        var repoRelPaths = new List<string>();
        foreach (var seg in new[] { "DataConfigs/Data/RoleData", "DataConfigs/Text/RoleData" })
        {
            var dirRel = string.IsNullOrEmpty(projRelNorm) ? seg : projRelNorm + "/" + seg;
            repoRelPaths.AddRange(EnumerateCsvBlobRepoRelativePathsUnderDirectory(tip, dirRel));
        }

        if (repoRelPaths.Count == 0)
        {
            var samplePrefix = string.IsNullOrEmpty(projRelNorm) ? "DataConfigs" : projRelNorm + "/DataConfigs";
            return (false,
                "远端跟踪分支中未找到角色 CSV。\n"
                + $"期望路径前缀：{samplePrefix}/Data/RoleData 与 {samplePrefix}/Text/RoleData\n"
                + "请确认：① 工程根是否选错（应与 Unity 工程根一致）；② 角色表是否已提交到该分支。");
        }

        var utf8 = new UTF8Encoding(false);
        var written = 0;
        var skipped = new List<string>();
        foreach (var rel in repoRelPaths)
        {
            var treePath = rel.Replace('\\', '/');
            var entry = tip[treePath];
            if (entry == null || entry.TargetType != TreeEntryTargetType.Blob)
            {
                skipped.Add(treePath);
                continue;
            }

            var blob = (Blob)entry.Target;
            var abs = Path.GetFullPath(Path.Combine(workRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, blob.GetContentText(utf8), utf8);
            written++;
        }

        if (written == 0)
        {
            return (false,
                "未能写出任何角色 CSV。"
                + (skipped.Count > 0 ? "\n未在远端树中找到对应 blob：" + string.Join(", ", skipped.Take(8)) : string.Empty));
        }

        return (true, null);
    }

    /// <summary>列出某目录树下的 *.csv 的仓库相对路径（正斜杠）。</summary>
    private static IEnumerable<string> EnumerateCsvBlobRepoRelativePathsUnderDirectory(Commit commit, string directoryPathInRepo)
    {
        var tree = GetTreeAtPath(commit, directoryPathInRepo);
        if (tree == null)
        {
            yield break;
        }

        var prefix = directoryPathInRepo.Replace('\\', '/').TrimEnd('/');
        foreach (var e in tree)
        {
            if (e.TargetType == TreeEntryTargetType.Blob && e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                yield return prefix + "/" + e.Name;
            }
        }
    }

    private static Tree? GetTreeAtPath(Commit commit, string directoryPathInRepo)
    {
        var parts = directoryPathInRepo.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        Tree current = commit.Tree;
        foreach (var part in parts)
        {
            var te = current[part];
            if (te == null || te.TargetType != TreeEntryTargetType.Tree)
            {
                return null;
            }

            current = (Tree)te.Target;
        }

        return current;
    }

    /// <summary>将当前分支硬重置到上游跟踪分支顶端（丢弃未推送的提交，并使工作区与远端该提交一致）。</summary>
    public static (bool Ok, string? Error) ResetHardToUpstream(string workDir)
    {
        var root = Repository.Discover(workDir);
        if (string.IsNullOrEmpty(root))
        {
            return (false, "未找到 Git 仓库。");
        }

        using var repo = new Repository(root);
        var head = repo.Head;
        if (!head.IsTracking || head.TrackedBranch == null)
        {
            return (false, "当前分支未设置上游，无法对齐远端。");
        }

        var tip = head.TrackedBranch.Tip;
        if (tip == null)
        {
            return (false, "无法读取远端提交。");
        }

        try
        {
            repo.Reset(ResetMode.Hard, tip);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        return (true, null);
    }

    private static bool HasStagedChanges(Repository repo)
    {
        foreach (var entry in repo.RetrieveStatus())
        {
            var s = entry.State;
            if (s == FileStatus.NewInIndex || s == FileStatus.ModifiedInIndex || s == FileStatus.DeletedFromIndex
                || s == FileStatus.RenamedInIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatGitHttpError(Exception ex, string workDir)
    {
        var msg = ex.Message;
        var remote = GetPrimaryRemoteUrl(workDir);
        if (!string.IsNullOrWhiteSpace(remote))
        {
            msg += "\n远端：" + remote;
        }

        if (msg.Contains("404", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            msg += "\n提示：Fetch 返回 404 通常与 OAuth 无关（登录已成功）。请核对：① git remote -v 的地址在浏览器能否打开；② 仓库是否改名/迁移；③ 私有仓库时当前账号是否有权访问（GitHub 对无权限常返回 404 而非 403）。";
        }

        return msg;
    }

    private static FetchOptions CreateFetchOptions(GitHubCredentials? creds)
    {
        var fo = new FetchOptions();
        if (creds != null && !string.IsNullOrWhiteSpace(creds.PersonalAccessToken))
        {
            fo.CredentialsProvider = (url, userFromUrl, _types) =>
                new UsernamePasswordCredentials
                {
                    Username = ResolveCredentialUserName(url, creds, userFromUrl),
                    Password = creds.PersonalAccessToken
                };
        }

        return fo;
    }

    private static PushOptions CreatePushOptions(GitHubCredentials? creds)
    {
        var po = new PushOptions();
        if (creds != null && !string.IsNullOrWhiteSpace(creds.PersonalAccessToken))
        {
            po.CredentialsProvider = (url, userFromUrl, _types) =>
                new UsernamePasswordCredentials
                {
                    Username = ResolveCredentialUserName(url, creds, userFromUrl),
                    Password = creds.PersonalAccessToken
                };
        }

        return po;
    }

    private static string ResolveCredentialUserName(string url, GitHubCredentials creds, string? userFromUrl)
    {
        if (!string.IsNullOrWhiteSpace(creds.UserName))
        {
            return creds.UserName.Trim();
        }

        if (url.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return "x-access-token";
        }

        return string.IsNullOrWhiteSpace(userFromUrl) ? "git" : userFromUrl;
    }

    private static Signature BuildSignature(Repository repo)
    {
        return repo.Config.BuildSignature(DateTimeOffset.Now)
               ?? new Signature("VNEditor", "vneditor@local", DateTimeOffset.Now);
    }

    private static string? GetRepoRelativePath(string repoWorkRoot, string absolutePath)
    {
        var full = Path.GetFullPath(absolutePath);
        var root = repoWorkRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rel = Path.GetRelativePath(root, full);
        return string.IsNullOrEmpty(rel) ? null : rel;
    }
}
