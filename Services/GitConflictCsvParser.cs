using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace VNEditor.Services;

/// <summary>从含 Git 冲突标记的文本中提取 CSV 首列 Id，用于在行上显示警告。</summary>
internal static class GitConflictCsvParser
{
    private static readonly Regex BlockRx = new(
        @"^<<<<<<<[^\r\n]*\r?\n([\s\S]*?)^=======\r?\n([\s\S]*?)^>>>>>>>[^\r\n]*\r?\n?",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static HashSet<(string SceneName, string IdPart)> CollectLineWarnings(string relativePath, string fileText)
    {
        var set = new HashSet<(string, string)>();
        if (string.IsNullOrEmpty(fileText) || !fileText.Contains("<<<<<<<", StringComparison.Ordinal))
        {
            return set;
        }

        var scene = Path.GetFileNameWithoutExtension(relativePath.Replace('\\', '/').Split('/')[^1]);
        if (string.IsNullOrEmpty(scene))
        {
            return set;
        }

        foreach (Match m in BlockRx.Matches(fileText))
        {
            AddIdsFromChunk(scene, m.Groups[1].Value, set);
            AddIdsFromChunk(scene, m.Groups[2].Value, set);
        }

        return set;
    }

    private static void AddIdsFromChunk(string sceneName, string chunk, ISet<(string, string)> set)
    {
        var rows = CsvUtility.ReadAllRowsFromString(chunk);
        foreach (var row in rows)
        {
            if (row.Length == 0)
            {
                continue;
            }

            var raw = row[0].Trim();
            if (string.IsNullOrEmpty(raw) || raw.StartsWith("<<<<<<<", StringComparison.Ordinal)
                                          || raw.StartsWith("=======", StringComparison.Ordinal)
                                          || raw.StartsWith(">>>>>>>", StringComparison.Ordinal))
            {
                continue;
            }

            var idPart = DialogueProjectService.NormalizeLineIdPartForGit(sceneName, raw);
            if (!string.IsNullOrEmpty(idPart))
            {
                set.Add((sceneName, idPart));
            }
        }
    }
}
