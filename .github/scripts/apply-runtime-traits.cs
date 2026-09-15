// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Inserts [Trait("Runtime", <lane>)] above each class named in a runtime lane report.
// Usage: dotnet run apply-runtime-traits.cs <report.tsv> <source-root>

using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run apply-runtime-traits.cs <report.tsv> <source-root>");
    return 2;
}

const string trait = "Runtime";
var bomBytes = new byte[] { 0xEF, 0xBB, 0xBF };

var classHead = new Regex(@"^(\s*)(?:public|internal)\s+(?:sealed\s+|partial\s+)*class\b(.*)$");
var bareName = new Regex(@"^\s*([A-Za-z0-9_]+)");
var nsPattern = new Regex(@"^\s*namespace\s+([A-Za-z0-9_.]+)\s*;?\s*$");

string reportPath = args[0];
string sourceRoot = args[1];

var reportEntries = ParseReport(reportPath);
var index = IndexDeclarations(sourceRoot);
var edits = new Dictionary<string, FileEdit>();
var missing = new List<string>();
var skipped = 0;

foreach (var (fullName, lane) in reportEntries)
{
    var dot = fullName.LastIndexOf('.');
    var ns = dot >= 0 ? fullName[..dot] : "";
    var simple = dot >= 0 ? fullName[(dot + 1)..] : fullName;
    var key = (ns, simple);

    if (!index.TryGetValue(key, out var location))
    {
        missing.Add(fullName);
        continue;
    }

    if (!edits.TryGetValue(location.Path, out var entry))
    {
        var (lines, hadBom, newLine) = ReadSource(location.Path);
        entry = new FileEdit(lines, hadBom, newLine, false);
    }

    var match = ClassDeclarationsWithNs(entry.Lines).FirstOrDefault(d => d.Namespace == ns && d.Name == simple);
    if (match.Name is null)
    {
        missing.Add(fullName);
        edits[location.Path] = entry;
        continue;
    }

    if (AlreadyTraited(entry.Lines, match.Index))
    {
        skipped++;
        edits[location.Path] = entry;
        continue;
    }

    var traitLine = $"{match.Indent}[Trait(\"{trait}\", \"{lane}\")]";
    entry.Lines.Insert(match.Index, traitLine);
    edits[location.Path] = entry with { Dirty = true };
}

var changed = 0;
foreach (var (path, entry) in edits)
{
    if (!entry.Dirty)
    {
        continue;
    }

    changed++;
    WriteSource(path, entry.Lines, entry.HadBom, entry.NewLine);
}

Console.WriteLine($"classes in report: {reportEntries.Count}");
Console.WriteLine($"files changed: {changed}");
Console.WriteLine($"already traited: {skipped}");
if (missing.Count > 0)
{
    Console.WriteLine($"NOT FOUND in source ({missing.Count}):");
    foreach (var name in missing)
    {
        Console.WriteLine("  " + name);
    }

    return 1;
}

return 0;

List<(string FullName, string Lane)> ParseReport(string path)
{
    var entries = new List<(string, string)>();
    foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var parts = line.Split('\t');
        if (parts.Length < 2)
        {
            continue;
        }

        var lane = parts[1];
        if (lane == "Core" || lane == "Framework")
        {
            entries.Add((parts[0], lane));
        }
    }

    return entries;
}

(List<string> Lines, bool HadBom, string NewLine) ReadSource(string path)
{
    var raw = File.ReadAllBytes(path);
    var hadBom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
    var text = new UTF8Encoding(false).GetString(hadBom ? raw.AsSpan(3) : raw.AsSpan());
    var newLine = text.Contains("\r\n") ? "\r\n" : "\n";
    return (SplitLines(text), hadBom, newLine);
}

List<string> SplitLines(string text)
{
    if (text.Length == 0)
    {
        return new List<string>();
    }

    var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
    var parts = normalized.Split('\n').ToList();
    if (normalized.EndsWith('\n'))
    {
        parts.RemoveAt(parts.Count - 1);
    }

    return parts;
}

void WriteSource(string path, List<string> lines, bool hadBom, string newLine)
{
    var text = string.Join(newLine, lines) + newLine;
    var bytes = new UTF8Encoding(false).GetBytes(text);
    if (hadBom)
    {
        bytes = bomBytes.Concat(bytes).ToArray();
    }

    File.WriteAllBytes(path, bytes);
}

IEnumerable<(int Index, string Indent, string Name)> FindClassDeclarations(List<string> lines)
{
    for (var i = 0; i < lines.Count; i++)
    {
        var m = classHead.Match(lines[i]);
        if (!m.Success)
        {
            continue;
        }

        var indent = m.Groups[1].Value;
        var rest = m.Groups[2].Value.Trim();
        if (rest.Length > 0)
        {
            var nameM = bareName.Match(rest);
            if (nameM.Success)
            {
                yield return (i, indent, nameM.Groups[1].Value);
            }

            continue;
        }

        if (i + 1 < lines.Count)
        {
            var nameM = bareName.Match(lines[i + 1]);
            if (nameM.Success)
            {
                yield return (i, indent, nameM.Groups[1].Value);
            }
        }
    }
}

IEnumerable<(int Index, string Indent, string? Namespace, string Name)> ClassDeclarationsWithNs(List<string> lines)
{
    string? currentNs = null;
    var nsAtLine = new string?[lines.Count];
    for (var i = 0; i < lines.Count; i++)
    {
        var m = nsPattern.Match(lines[i]);
        if (m.Success)
        {
            currentNs = m.Groups[1].Value;
        }

        nsAtLine[i] = currentNs;
    }

    foreach (var (i, indent, name) in FindClassDeclarations(lines))
    {
        yield return (i, indent, nsAtLine[i], name);
    }
}

Dictionary<(string Namespace, string Name), (string Path, int Index)> IndexDeclarations(string root)
{
    var result = new Dictionary<(string, string), (string, int)>();
    var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
        .Where(p => !p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "obj" or "bin"))
        .OrderBy(p => p, StringComparer.Ordinal);

    foreach (var path in files)
    {
        var (lines, _, _) = ReadSource(path);
        foreach (var (i, _, ns, name) in ClassDeclarationsWithNs(lines))
        {
            if (ns is not null)
            {
                result.TryAdd((ns, name), (path, i));
            }
        }
    }

    return result;
}

bool AlreadyTraited(List<string> lines, int declIndex)
{
    var i = declIndex - 1;
    while (i >= 0)
    {
        var stripped = lines[i].Trim();
        if (stripped.StartsWith('['))
        {
            if (stripped.Contains("RuntimeLaneResolver.TraitName") || stripped.Contains($"\"{trait}\""))
            {
                return true;
            }

            i--;
            continue;
        }

        if (stripped.Length == 0 || stripped.StartsWith("//"))
        {
            i--;
            continue;
        }

        return false;
    }

    return false;
}

internal record FileEdit(List<string> Lines, bool HadBom, string NewLine, bool Dirty);
