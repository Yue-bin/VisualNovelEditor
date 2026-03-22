using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace VNEditor.Services;

public static class CsvUtility
{
    public static List<string[]> ReadAllRows(string path)
    {
        var text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                }
                else if (c == '\r')
                {
                    // ignore
                }
                else if (c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row.ToArray());
                    row = new List<string>();
                }
                else
                {
                    cell.Append(c);
                }
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }

    public static List<string[]> ReadAllRowsFromString(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string[]>();
        }

        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                }
                else if (c == '\r')
                {
                    // ignore
                }
                else if (c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row.ToArray());
                    row = new List<string>();
                }
                else
                {
                    cell.Append(c);
                }
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }

    public static void WriteAllRows(string path, IEnumerable<string[]> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        foreach (var row in rows)
        {
            var escaped = row.Select(EscapeCell);
            writer.WriteLine(string.Join(",", escaped));
        }
    }

    private static string EscapeCell(string? cell)
    {
        var value = cell ?? string.Empty;
        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
