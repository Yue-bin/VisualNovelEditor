using System;
using System.Collections.Generic;
using System.IO;

namespace VNEditor.Services;

/// <summary>对话背景、立绘等相对路径解析（支持 Assets 与 DataConfigs 镜像目录）。</summary>
public static class ResourcePathResolver
{
    /// <param name="projectRoot">含 DataConfigs、与 .git 同级的工程根。</param>
    /// <param name="resourcesRoot">通常为 Assets/Resources。</param>
    /// <param name="gameResourcesRoot">通常为 Assets/GameResources。</param>
    public static string Resolve(
        string? rawPath,
        string projectRoot,
        string resourcesRoot,
        string gameResourcesRoot)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(rawPath))
        {
            return rawPath;
        }

        if (File.Exists(rawPath))
        {
            return Path.GetFullPath(rawPath);
        }

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
        {
            candidates.Add(Path.Combine(AppContext.BaseDirectory, rawPath));
        }

        if (!string.IsNullOrWhiteSpace(Environment.CurrentDirectory))
        {
            candidates.Add(Path.Combine(Environment.CurrentDirectory, rawPath));
        }

        if (!string.IsNullOrWhiteSpace(resourcesRoot))
        {
            candidates.Add(Path.Combine(resourcesRoot, rawPath));
        }

        if (!string.IsNullOrWhiteSpace(gameResourcesRoot))
        {
            candidates.Add(Path.Combine(gameResourcesRoot, rawPath));
        }

        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            candidates.Add(Path.Combine(projectRoot, "DataConfigs", "Resources", rawPath));
            candidates.Add(Path.Combine(projectRoot, "DataConfigs", "GameResources", rawPath));
            candidates.Add(Path.Combine(projectRoot, rawPath));
            var assetsRoot = Path.Combine(projectRoot, "Assets");
            if (Directory.Exists(assetsRoot))
            {
                candidates.Add(Path.Combine(assetsRoot, rawPath));
                candidates.Add(Path.Combine(assetsRoot, "Resources", rawPath));
                candidates.Add(Path.Combine(assetsRoot, "GameResources", rawPath));
            }
        }

        foreach (var candidate in candidates)
        {
            var found = ExpandCandidate(candidate);
            if (!string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }

        return string.Empty;
    }

    private static string ExpandCandidate(string pathNoExt)
    {
        if (File.Exists(pathNoExt))
        {
            return pathNoExt;
        }

        var exts = new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };
        foreach (var ext in exts)
        {
            var p = pathNoExt + ext;
            if (File.Exists(p))
            {
                return p;
            }
        }

        return string.Empty;
    }
}
