using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using VNEditor.Models;

namespace VNEditor.Services;

public static class DialogueProjectService
{
    private const string BgMetaPrefix = "//VNEditor:BG=";

    /// <summary>对话 CSV：&lt;工程根&gt;/DataConfigs/Data/Dialogue（与 .git 同级的是工程根）。</summary>
    public static string GetDialogueDataDir(string projectRoot) =>
        Path.Combine(projectRoot, "DataConfigs", "Data", "Dialogue");

    /// <summary>对话 CSV：&lt;工程根&gt;/DataConfigs/Text/Dialogue。</summary>
    public static string GetDialogueTextDir(string projectRoot) =>
        Path.Combine(projectRoot, "DataConfigs", "Text", "Dialogue");

    public static string GetRoleDataDataDir(string projectRoot) =>
        Path.Combine(projectRoot, "DataConfigs", "Data", "RoleData");

    public static string GetRoleDataTextDir(string projectRoot) =>
        Path.Combine(projectRoot, "DataConfigs", "Text", "RoleData");

    /// <summary>
    /// 解析工程：在选中路径下定位 <c>DataConfigs/Data/Dialogue</c> 与 <c>DataConfigs/Text/Dialogue</c>，
    /// 返回的 <c>projectRoot</c> 为包含 <c>DataConfigs</c> 的目录（与仓库 .git 同级）。
    /// </summary>
    public static (string dataDir, string textDir, string projectRoot)? ResolveProjectDirs(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
        {
            return null;
        }

        var full = Path.GetFullPath(selectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        // 1) 选中文件夹即为 DataConfigs
        if (Path.GetFileName(full).Equals("DataConfigs", StringComparison.OrdinalIgnoreCase))
        {
            var projectRoot = Directory.GetParent(full)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            var dataDir = Path.Combine(full, "Data", "Dialogue");
            var textDir = Path.Combine(full, "Text", "Dialogue");
            if (Directory.Exists(dataDir) && Directory.Exists(textDir))
            {
                return (dataDir, textDir, projectRoot);
            }

            return null;
        }

        // 2) 工程根：直接包含 DataConfigs 子目录
        var childDataConfigs = Path.Combine(full, "DataConfigs");
        if (Directory.Exists(childDataConfigs))
        {
            var dataDir = Path.Combine(childDataConfigs, "Data", "Dialogue");
            var textDir = Path.Combine(childDataConfigs, "Text", "Dialogue");
            if (Directory.Exists(dataDir) && Directory.Exists(textDir))
            {
                return (dataDir, textDir, full);
            }

            return null;
        }

        // 3) 向上查找：某级目录下存在 DataConfigs，或当前位于 DataConfigs 子树内
        for (var dir = new DirectoryInfo(full); dir != null; dir = dir.Parent)
        {
            var tryDc = Path.Combine(dir.FullName, "DataConfigs");
            if (Directory.Exists(tryDc))
            {
                var dataDir = Path.Combine(tryDc, "Data", "Dialogue");
                var textDir = Path.Combine(tryDc, "Text", "Dialogue");
                if (Directory.Exists(dataDir) && Directory.Exists(textDir))
                {
                    return (dataDir, textDir, dir.FullName);
                }
            }

            if (dir.Name.Equals("DataConfigs", StringComparison.OrdinalIgnoreCase))
            {
                var dataDir = Path.Combine(dir.FullName, "Data", "Dialogue");
                var textDir = Path.Combine(dir.FullName, "Text", "Dialogue");
                if (Directory.Exists(dataDir) && Directory.Exists(textDir))
                {
                    var projectRoot = dir.Parent?.FullName;
                    if (!string.IsNullOrEmpty(projectRoot))
                    {
                        return (dataDir, textDir, projectRoot);
                    }
                }

                return null;
            }
        }

        return null;
    }

    public static ObservableCollection<DialogueScene> LoadScenes(string dataDir, string textDir)
    {
        var scenes = new ObservableCollection<DialogueScene>();

        var dataFiles = BuildCsvMap(dataDir);
        var textFiles = BuildCsvMap(textDir);

        var sceneNames = dataFiles.Keys.Union(textFiles.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        foreach (var sceneName in sceneNames)
        {
            dataFiles.TryGetValue(sceneName, out var dataPath);
            textFiles.TryGetValue(sceneName, out var textPath);
            scenes.Add(LoadScene(sceneName, dataPath, textPath));
        }

        return scenes;
    }

    public static void ExportScenes(IEnumerable<DialogueScene> scenes, string outputRoot)
    {
        var dataDir = GetDialogueDataDir(outputRoot);
        var textDir = GetDialogueTextDir(outputRoot);
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(textDir);

        foreach (var scene in scenes)
        {
            ExportScene(scene, dataDir, textDir);
        }
    }

    public static void ExportScene(
        DialogueScene scene,
        string dataDir,
        string textDir,
        ISet<string>? validRoleIds = null)
    {
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(textDir);

        var maxChoice = Math.Clamp(scene.Lines.Count == 0 ? 0 : scene.Lines.Max(x => x.ChoiceCount), 0, 4);
        var dataRows = new List<string[]> { BuildDataHeader(maxChoice), BuildDataDesc(maxChoice) };
        var textRows = new List<string[]> { BuildTextHeader(maxChoice), BuildTextDesc(maxChoice) };

        foreach (var line in scene.Lines)
        {
            var baseScript = BuildScriptWithMetadata(line.BaseScript, line.BackgroundPath);
            var dataRow = new List<string>
            {
                line.CsvId,
                baseScript,
                line.EndScript,
                NormalizeRolesForExport(line.Roles, validRoleIds),
                line.IsNarrator ? "TRUE" : "FALSE",
                line.EventName,
                line.ChoiceCount.ToString()
            };
            for (var i = 1; i <= maxChoice; i++)
            {
                dataRow.Add(GetChoiceScript(line, i));
            }
            dataRows.Add(dataRow.ToArray());

            var textRow = new List<string>
            {
                line.CsvId,
                line.Text,
                line.TextEn,
                line.TextJa
            };
            for (var i = 1; i <= maxChoice; i++)
            {
                textRow.Add(GetChoiceText(line, i, "zh"));
                textRow.Add(GetChoiceText(line, i, "en"));
                textRow.Add(GetChoiceText(line, i, "ja"));
            }
            textRow.Add(string.Empty);
            textRow.Add(string.Empty);
            textRow.Add(string.Empty);
            textRows.Add(textRow.ToArray());
        }

        CsvUtility.WriteAllRows(Path.Combine(dataDir, $"{scene.Name}.csv"), dataRows);
        CsvUtility.WriteAllRows(Path.Combine(textDir, $"{scene.Name}.csv"), textRows);
    }

    public static Dictionary<string, string> LoadRoleCharacterMap(string projectRoot)
    {
        var roleDir = GetRoleDataDataDir(projectRoot);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(roleDir))
        {
            return map;
        }

        foreach (var file in Directory.GetFiles(roleDir, "*.csv"))
        {
            var rows = CsvUtility.ReadAllRows(file);
            if (rows.Count < 3)
            {
                continue;
            }

            var header = rows[0];
            var idIdx = FindColumn(header, "Id");
            var charIdx = FindColumn(header, "CharacterImage");
            if (idIdx < 0 || charIdx < 0)
            {
                continue;
            }

            for (var i = 2; i < rows.Count; i++)
            {
                var row = rows[i];
                var id = GetCell(row, idIdx);
                var img = GetCell(row, charIdx);
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(img))
                {
                    continue;
                }

                map[id] = img;
                map[$"role_{id}"] = img;
            }
        }

        return map;
    }

    public static Dictionary<string, string> LoadRoleNameMap(string projectRoot)
    {
        var roleDir = GetRoleDataTextDir(projectRoot);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(roleDir))
        {
            return map;
        }

        foreach (var file in Directory.GetFiles(roleDir, "*.csv"))
        {
            var rows = CsvUtility.ReadAllRows(file);
            if (rows.Count < 3)
            {
                continue;
            }

            var header = rows[0];
            var idIdx = FindColumn(header, "Id");
            var nameIdx = FindColumn(header, "Name");
            if (idIdx < 0 || nameIdx < 0)
            {
                continue;
            }

            for (var i = 2; i < rows.Count; i++)
            {
                var row = rows[i];
                var id = GetCell(row, idIdx);
                var name = GetCell(row, nameIdx);
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                map[id] = name;
                map[$"role_{id}"] = name;
            }
        }

        return map;
    }

    public static List<RoleEntry> LoadRoleEntries(string projectRoot)
    {
        var dataDir = GetRoleDataDataDir(projectRoot);
        var textDir = GetRoleDataTextDir(projectRoot);
        var dataFiles = BuildCsvMap(dataDir);
        var textFiles = BuildCsvMap(textDir);
        var categories = dataFiles.Keys.Union(textFiles.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var result = new List<RoleEntry>();

        foreach (var category in categories)
        {
            dataFiles.TryGetValue(category, out var dataPath);
            textFiles.TryGetValue(category, out var textPath);
            var roleMap = new Dictionary<string, RoleEntry>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(dataPath) && File.Exists(dataPath))
            {
                var rows = CsvUtility.ReadAllRows(dataPath);
                if (rows.Count >= 3)
                {
                    var header = rows[0];
                    for (var i = 2; i < rows.Count; i++)
                    {
                        var row = rows[i];
                        var id = GetCellByColumn(row, header, "Id");
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        if (!roleMap.TryGetValue(id, out var role))
                        {
                            role = new RoleEntry
                            {
                                Category = category,
                                Id = id
                            };
                            roleMap[id] = role;
                        }

                        role.Avatar = GetCellByColumn(row, header, "Avatar");
                        role.CharacterImage = GetCellByColumn(row, header, "CharacterImage");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(textPath) && File.Exists(textPath))
            {
                var rows = CsvUtility.ReadAllRows(textPath);
                if (rows.Count >= 3)
                {
                    var header = rows[0];
                    for (var i = 2; i < rows.Count; i++)
                    {
                        var row = rows[i];
                        var id = GetCellByColumn(row, header, "Id");
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        if (!roleMap.TryGetValue(id, out var role))
                        {
                            role = new RoleEntry
                            {
                                Category = category,
                                Id = id
                            };
                            roleMap[id] = role;
                        }

                        role.Name = GetCellByColumn(row, header, "Name");
                        role.NameEn = GetCellByColumn(row, header, "Name_en");
                        role.NameZhHant = GetCellByColumn(row, header, "Name_zh-Hant");
                        role.NameJa = GetCellByColumn(row, header, "Name_ja");
                    }
                }
            }

            result.AddRange(roleMap.Values.OrderBy(x => x.Id));
        }

        return result;
    }

    /// <summary>
    /// 保存角色表。会合并磁盘已有数据：内存中<strong>至少有一个角色</strong>的分类以内存为准（含删改）；
    /// 内存中<strong>完全没有</strong>某分类时，保留该分类在磁盘上的 CSV（避免加载不完整时一次保存清空其它分类）。
    /// 删除整分类请用 <see cref="DeleteRoleCategoryCsvFiles"/>。
    /// </summary>
    public static void SaveRoleEntries(string projectRoot, IEnumerable<RoleEntry> roles)
    {
        var dataDir = GetRoleDataDataDir(projectRoot);
        var textDir = GetRoleDataTextDir(projectRoot);
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(textDir);

        var mem = roles.Where(r => !string.IsNullOrWhiteSpace(r.Id)).ToList();
        List<RoleEntry> disk;
        try
        {
            disk = LoadRoleEntries(projectRoot);
        }
        catch
        {
            disk = new List<RoleEntry>();
        }

        var memCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in mem)
        {
            memCats.Add(NormalizeCategoryName(string.IsNullOrWhiteSpace(r.Category) ? InferCategoryFromId(r.Id) : r.Category));
        }

        var merged = new List<RoleEntry>(mem);
        foreach (var r in disk)
        {
            var c = NormalizeCategoryName(string.IsNullOrWhiteSpace(r.Category) ? InferCategoryFromId(r.Id) : r.Category);
            if (!memCats.Contains(c))
            {
                merged.Add(r);
            }
        }

        var grouped = merged
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Category) ? InferCategoryFromId(r.Id) : r.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var category = NormalizeCategoryName(group.Key);
            var ordered = group.OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase).ToList();

            var dataRows = new List<string[]>
            {
                new[] { "Id", "Avatar", "CharacterImage" },
                new[] { "唯一标识", "头像路径", "立绘路径" }
            };
            foreach (var role in ordered)
            {
                dataRows.Add(new[] { role.Id, role.Avatar, role.CharacterImage });
            }

            var textRows = new List<string[]>
            {
                new[] { "Id", "Name", "Name_en", "Name_zh-Hant", "Name_ja" },
                new[] { "唯一标识", "", "", "", "" }
            };
            foreach (var role in ordered)
            {
                textRows.Add(new[] { role.Id, role.Name, role.NameEn, role.NameZhHant, role.NameJa });
            }

            CsvUtility.WriteAllRows(Path.Combine(dataDir, $"{category}.csv"), dataRows);
            CsvUtility.WriteAllRows(Path.Combine(textDir, $"{category}.csv"), textRows);
        }
    }

    /// <summary>删除某分类在 Data/Text RoleData 下对应的两个 CSV（「删除分类」按钮）。</summary>
    public static void DeleteRoleCategoryCsvFiles(string projectRoot, string category)
    {
        var c = NormalizeCategoryName(category);
        var dataDir = GetRoleDataDataDir(projectRoot);
        var textDir = GetRoleDataTextDir(projectRoot);
        var dataPath = Path.Combine(dataDir, $"{c}.csv");
        var textPath = Path.Combine(textDir, $"{c}.csv");
        try
        {
            if (File.Exists(dataPath))
            {
                File.Delete(dataPath);
            }

            if (File.Exists(textPath))
            {
                File.Delete(textPath);
            }
        }
        catch
        {
            // 忽略删除失败，避免阻断 UI
        }
    }

    private static string InferCategoryFromId(string roleId)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return "role";
        }

        var idx = roleId.IndexOf('_');
        return idx > 0 ? roleId[..idx] : "role";
    }

    private static string NormalizeCategoryName(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "role";
        }

        var trimmed = category.Trim();
        foreach (var ch in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(ch, '_');
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "role" : trimmed;
    }

    /// <summary>供 Git 冲突行匹配：将 CSV 首列 Id 规范为与 <see cref="DialogueLine.IdPart"/> 一致。</summary>
    public static string NormalizeLineIdPartForGit(string sceneName, string originalIdFromCsv)
    {
        return NormalizeRawId(sceneName, originalIdFromCsv);
    }

    /// <summary>从磁盘重新加载单个场景（与启动时 LoadScenes 规则一致）。</summary>
    public static DialogueScene LoadSceneFromDisk(string sceneName, string? dataPath, string? textPath)
    {
        return LoadScene(sceneName, dataPath, textPath);
    }

    private static DialogueScene LoadScene(string sceneName, string? dataPath, string? textPath)
    {
        var scene = new DialogueScene { Name = sceneName };
        var lines = new Dictionary<string, DialogueLine>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        if (!string.IsNullOrEmpty(dataPath) && File.Exists(dataPath))
        {
            var rows = CsvUtility.ReadAllRows(dataPath);
            if (rows.Count >= 3)
            {
                var header = rows[0];
                for (var i = 2; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var originalId = GetCell(row, 0);
                    if (string.IsNullOrWhiteSpace(originalId))
                    {
                        continue;
                    }

                    var key = NormalizeRawId(sceneName, originalId);
                    var line = GetOrCreateLine(lines, key, order);
                    line.IdPart = key;

                    var rawBaseScript = GetCell(row, 1);
                    var (pureBaseScript, bg) = ExtractMetadataFromScript(rawBaseScript);
                    line.BaseScript = pureBaseScript;
                    line.BackgroundPath = bg;
                    line.EndScript = GetCellByColumn(row, header, "EndScript");
                    line.Roles = GetCellByColumn(row, header, "Roles");
                    line.IsNarrator = ToBool(GetCellByColumn(row, header, "IsNarrator"));
                    line.EventName = GetCellByColumn(row, header, "EventName");
                    line.ChoiceCount = ToInt(GetCellByColumn(row, header, "ChoiceCount"));
                    line.ChoiceScript1 = GetCellByColumn(row, header, "ChoiceScript1");
                    line.ChoiceScript2 = GetCellByColumn(row, header, "ChoiceScript2");
                    line.ChoiceScript3 = GetCellByColumn(row, header, "ChoiceScript3");
                    line.ChoiceScript4 = GetCellByColumn(row, header, "ChoiceScript4");
                }
            }
        }

        if (!string.IsNullOrEmpty(textPath) && File.Exists(textPath))
        {
            var rows = CsvUtility.ReadAllRows(textPath);
            if (rows.Count >= 3)
            {
                var header = rows[0];
                for (var i = 2; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var originalId = GetCell(row, 0);
                    if (string.IsNullOrWhiteSpace(originalId))
                    {
                        continue;
                    }

                    var key = NormalizeRawId(sceneName, originalId);
                    var line = GetOrCreateLine(lines, key, order);
                    line.IdPart = key;
                    line.Text = GetCell(row, 1);
                    line.TextEn = GetCell(row, 2);
                    line.TextJa = GetCell(row, 3);
                    line.ChoiceText1 = GetCellByColumn(row, header, "ChoiceText1");
                    line.ChoiceText1En = GetCellByColumn(row, header, "ChoiceText1_en");
                    line.ChoiceText1Ja = GetCellByColumn(row, header, "ChoiceText1_ja");
                    line.ChoiceText2 = GetCellByColumn(row, header, "ChoiceText2");
                    line.ChoiceText2En = GetCellByColumn(row, header, "ChoiceText2_en");
                    line.ChoiceText2Ja = GetCellByColumn(row, header, "ChoiceText2_ja");
                    line.ChoiceText3 = GetCellByColumn(row, header, "ChoiceText3");
                    line.ChoiceText3En = GetCellByColumn(row, header, "ChoiceText3_en");
                    line.ChoiceText3Ja = GetCellByColumn(row, header, "ChoiceText3_ja");
                    line.ChoiceText4 = GetCellByColumn(row, header, "ChoiceText4");
                    line.ChoiceText4En = GetCellByColumn(row, header, "ChoiceText4_en");
                    line.ChoiceText4Ja = GetCellByColumn(row, header, "ChoiceText4_ja");
                }
            }
        }

        foreach (var id in order)
        {
            scene.Lines.Add(lines[id]);
        }

        return scene;
    }

    private static DialogueLine GetOrCreateLine(
        IDictionary<string, DialogueLine> lines,
        string id,
        ICollection<string> order)
    {
        if (lines.TryGetValue(id, out var line))
        {
            return line;
        }

        line = new DialogueLine
        {
            IdPart = id,
            Roles = "role_narrator"
        };
        lines[id] = line;
        order.Add(id);
        return line;
    }

    private static Dictionary<string, string> BuildCsvMap(string folder)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder))
        {
            return map;
        }

        foreach (var file in Directory.GetFiles(folder, "*.csv"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrWhiteSpace(name))
            {
                map[name] = file;
            }
        }

        return map;
    }

    private static string NormalizeRawId(string sceneName, string originalId)
    {
        var temp = originalId.Trim();
        if (temp.StartsWith('*'))
        {
            temp = temp[1..];
        }

        var prefix = sceneName + "_";
        if (temp.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            temp = temp[prefix.Length..];
        }

        return temp;
    }

    private static (string pureScript, string bg) ExtractMetadataFromScript(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return (string.Empty, string.Empty);
        }

        var bg = string.Empty;
        var lines = script.Replace("\r\n", "\n").Split('\n');
        var pure = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(BgMetaPrefix, StringComparison.Ordinal))
            {
                bg = trimmed[BgMetaPrefix.Length..].Trim();
                continue;
            }

            pure.Add(line);
        }

        return (string.Join(Environment.NewLine, pure).Trim(), bg);
    }

    private static string BuildScriptWithMetadata(string baseScript, string bg)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(bg))
        {
            builder.AppendLine(BgMetaPrefix + bg.Trim());
        }

        if (!string.IsNullOrWhiteSpace(baseScript))
        {
            builder.Append(baseScript.Trim());
        }

        return builder.ToString();
    }

    private static int FindColumn(string[] header, string name)
    {
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetCell(string[] row, int index)
    {
        return index >= 0 && index < row.Length ? row[index] : string.Empty;
    }

    private static string GetCellByColumn(string[] row, string[] header, string column)
    {
        var idx = FindColumn(header, column);
        return GetCell(row, idx);
    }

    private static int ToInt(string value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }

    private static bool ToBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] BuildDataHeader(int maxChoice)
    {
        var cols = new List<string> { "Id", "BaseScript", "EndScript", "Roles", "IsNarrator", "EventName", "ChoiceCount" };
        for (var i = 1; i <= maxChoice; i++)
        {
            cols.Add($"ChoiceScript{i}");
        }
        return cols.ToArray();
    }

    private static string[] BuildDataDesc(int maxChoice)
    {
        var cols = new List<string>
        {
            "对话Id", "初始脚本", "对话结束时执行", "出现的角色Id，说话者用<>包括，用,分割", "是否旁白(TRUE/FALSE)", "何时执行对话（事件名）", "选项数量"
        };
        for (var i = 1; i <= maxChoice; i++)
        {
            cols.Add("选项脚本");
        }
        return cols.ToArray();
    }

    private static string[] BuildTextHeader(int maxChoice)
    {
        var cols = new List<string> { "Id", "Text", "Text_en", "Text_ja" };
        for (var i = 1; i <= maxChoice; i++)
        {
            cols.Add($"ChoiceText{i}");
            cols.Add($"ChoiceText{i}_en");
            cols.Add($"ChoiceText{i}_ja");
        }
        cols.Add("Notification");
        cols.Add("Notification_en");
        cols.Add("Notification_ja");
        return cols.ToArray();
    }

    private static string[] BuildTextDesc(int maxChoice)
    {
        var cols = new List<string> { "对话Id", "对话正文", "", "" };
        for (var i = 1; i <= maxChoice; i++)
        {
            cols.Add($"选项{i}文本");
            cols.Add("");
            cols.Add("");
        }
        cols.Add("");
        cols.Add("");
        cols.Add("");
        return cols.ToArray();
    }

    private static string GetChoiceScript(DialogueLine line, int index)
    {
        return index switch
        {
            1 => line.ChoiceScript1,
            2 => line.ChoiceScript2,
            3 => line.ChoiceScript3,
            4 => line.ChoiceScript4,
            _ => string.Empty
        };
    }

    private static string GetChoiceText(DialogueLine line, int index, string lang)
    {
        return (index, lang) switch
        {
            (1, "zh") => line.ChoiceText1,
            (1, "en") => line.ChoiceText1En,
            (1, "ja") => line.ChoiceText1Ja,
            (2, "zh") => line.ChoiceText2,
            (2, "en") => line.ChoiceText2En,
            (2, "ja") => line.ChoiceText2Ja,
            (3, "zh") => line.ChoiceText3,
            (3, "en") => line.ChoiceText3En,
            (3, "ja") => line.ChoiceText3Ja,
            (4, "zh") => line.ChoiceText4,
            (4, "en") => line.ChoiceText4En,
            (4, "ja") => line.ChoiceText4Ja,
            _ => string.Empty
        };
    }

    private static string NormalizeRolesForExport(string rolesRaw, ISet<string>? validRoleIds)
    {
        if (string.IsNullOrWhiteSpace(rolesRaw))
        {
            return string.Empty;
        }

        if (validRoleIds == null)
        {
            return rolesRaw;
        }

        var result = new List<string>();
        var tokens = rolesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var tokenRaw in tokens)
        {
            var token = tokenRaw.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var muted = token.StartsWith('*');
            if (muted)
            {
                token = token[1..].Trim();
            }

            if (string.IsNullOrWhiteSpace(token) || !IsRoleValid(token, validRoleIds))
            {
                continue;
            }

            result.Add((muted ? "*" : "") + token);
        }

        return string.Join(",", result);
    }

    private static bool IsRoleValid(string roleId, ISet<string> validRoleIds)
    {
        if (validRoleIds.Contains(roleId))
        {
            return true;
        }

        if (roleId.StartsWith("role_", StringComparison.OrdinalIgnoreCase))
        {
            return validRoleIds.Contains(roleId[5..]);
        }

        return validRoleIds.Contains("role_" + roleId);
    }
}
