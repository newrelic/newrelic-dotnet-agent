// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Reconciles [Trait("Platform", "WindowsOnly")] against a lane report, and removes
// every legacy [Trait("Runtime", ...)] line.
// Usage: dotnet run apply-platform-traits.cs <report.tsv> <source-root>

using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run apply-platform-traits.cs <report.tsv> <source-root>");
    return 2;
}

const string platformTrait = "Platform";
const string windowsOnly = "WindowsOnly";
const string portable = "Portable";
var bomBytes = new byte[] { 0xEF, 0xBB, 0xBF };

var classHead = new Regex(@"^(\s*)(?:public|internal)\s+(?:sealed\s+|partial\s+)*class\b(.*)$");
var bareName = new Regex(@"^\s*([A-Za-z0-9_]+)");
var nsPattern = new Regex(@"^\s*namespace\s+([A-Za-z0-9_.]+)\s*;?\s*$");

string reportPath = args[0];
string sourceRoot = args[1];

var reportEntries = ParseReport(reportPath, out var unclassifiable);
if (unclassifiable.Count > 0)
{
    Console.WriteLine($"REFUSING TO RUN. {unclassifiable.Count} row(s) carry no usable requirement:");
    foreach (var name in unclassifiable)
    {
        Console.WriteLine("  " + name);
    }

    Console.WriteLine("Fix the resolver, RuntimeTraitPolicy.ClassOverrides or ExemptClasses, then regenerate.");
    return 1;
}

var index = IndexDeclarations(sourceRoot);
var edits = new Dictionary<string, FileEdit>();
var missing = new List<string>();
var inserted = 0;
var removed = 0;
var legacyRemoved = 0;

foreach (var (fullName, requiresWindows) in reportEntries)
{
    var dot = fullName.LastIndexOf('.');
    var ns = dot >= 0 ? fullName[..dot] : "";
    var simple = dot >= 0 ? fullName[(dot + 1)..] : fullName;

    if (!index.TryGetValue((ns, simple), out var location))
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

    var dirty = entry.Dirty;

    var legacy = FindAttributeLine(entry.Lines, match.Index, "\"Runtime\"", "RuntimeLaneResolver.TraitName");
    if (legacy >= 0)
    {
        entry.Lines.RemoveAt(legacy);
        legacyRemoved++;
        dirty = true;
        match = ClassDeclarationsWithNs(entry.Lines).First(d => d.Namespace == ns && d.Name == simple);
    }

    var existing = FindAttributeLine(entry.Lines, match.Index, "\"Platform\"", "PlatformTrait.TraitName");
    if (requiresWindows && existing < 0)
    {
        entry.Lines.Insert(match.Index, $"{match.Indent}[Trait(\"{platformTrait}\", \"{windowsOnly}\")]");
        inserted++;
        dirty = true;
    }
    else if (!requiresWindows && existing >= 0)
    {
        entry.Lines.RemoveAt(existing);
        removed++;
        dirty = true;
    }

    edits[location.Path] = entry with { Dirty = dirty };
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
Console.WriteLine($"platform traits inserted: {inserted}");
Console.WriteLine($"platform traits removed: {removed}");
Console.WriteLine($"legacy runtime traits removed: {legacyRemoved}");
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

List<(string FullName, bool RequiresWindows)> ParseReport(string path, out List<string> unclassifiable)
{
    var entries = new List<(string, bool)>();
    unclassifiable = new List<string>();
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

        if (parts[1] == windowsOnly)
        {
            entries.Add((parts[0], true));
        }
        else if (parts[1] == portable)
        {
            entries.Add((parts[0], false));
        }
        else
        {
            unclassifiable.Add($"{parts[0]} -> \"{parts[1]}\"");
        }
    }

    return entries;
}

// Index of the attribute line matching any marker, in the attribute block directly
// above a class declaration. Returns -1 when there is none.
int FindAttributeLine(List<string> lines, int declIndex, params string[] markers)
{
    var i = declIndex - 1;
    while (i >= 0)
    {
        var stripped = lines[i].Trim();
        if (stripped.StartsWith('['))
        {
            if (markers.Any(m => stripped.Contains(m)))
            {
                return i;
            }

            i--;
            continue;
        }

        if (stripped.Length == 0 || stripped.StartsWith("//"))
        {
            i--;
            continue;
        }

        return -1;
    }

    return -1;
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

internal record FileEdit(List<string> Lines, bool HadBom, string NewLine, bool Dirty);
