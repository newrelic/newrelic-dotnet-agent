// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.Loader;

string? agentHome = null;
string? diffFile = null;
string? exclusionsFile = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--agent-home" && i + 1 < args.Length)
        agentHome = args[++i];
    else if (args[i] == "--diff" && i + 1 < args.Length)
        diffFile = args[++i];
    else if (args[i] == "--exclusions" && i + 1 < args.Length)
        exclusionsFile = args[++i];
}

agentHome ??= FindAgentHome();
exclusionsFile ??= FindExclusionsFile();

var exclusions = LoadExclusions(exclusionsFile);

if (agentHome == null || !Directory.Exists(agentHome))
{
    Console.Error.WriteLine("Could not find agent home. Build FullAgent.sln first, or pass --agent-home <path>.");
    return 1;
}

var coreDll = Path.Combine(agentHome, "NewRelic.Agent.Core.dll");
if (!File.Exists(coreDll))
{
    Console.Error.WriteLine($"NewRelic.Agent.Core.dll not found in: {agentHome}");
    return 1;
}

Console.Error.WriteLine($"Loading from: {agentHome}");

var alc = new AgentLoadContext(agentHome);
Assembly coreAssembly;
try
{
    coreAssembly = alc.LoadFromAssemblyPath(coreDll);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load Core.dll: {ex.Message}");
    return 1;
}

var metricNamesType = coreAssembly.GetType("NewRelic.Agent.Core.Metrics.MetricNames");
if (metricNamesType == null)
{
    Console.Error.WriteLine("Could not find MetricNames type. Was Core.dll built from current source?");
    return 1;
}

var discovered = new SortedSet<string>(StringComparer.Ordinal);
var unenumerable = new List<string>();

// --- Fields: const string and static readonly (MetricName or string) ---
foreach (var field in metricNamesType.GetFields(BindingFlags.Public | BindingFlags.Static))
{
    if (!field.IsLiteral && !field.IsInitOnly)
        continue;
    // Skip prefix constants (naming convention: field name ends with "Prefix")
    if (field.Name.EndsWith("Prefix", StringComparison.Ordinal))
        continue;

    object? value;
    try { value = field.GetValue(null); }
    catch { continue; }

    var str = value?.ToString();
    if (IsSupport(str))
        discovered.Add(str!);
}

// --- Properties: public static parameterless getters ---
foreach (var prop in metricNamesType.GetProperties(BindingFlags.Public | BindingFlags.Static))
{
    var getter = prop.GetMethod;
    if (getter == null || getter.GetParameters().Length > 0)
        continue;

    object? value;
    try { value = prop.GetValue(null); }
    catch { continue; }

    var str = value?.ToString();
    if (IsSupport(str))
        discovered.Add(str!);
}

// --- Methods: public static, all params enum or bool -> cross-product invoke ---
foreach (var method in metricNamesType.GetMethods(BindingFlags.Public | BindingFlags.Static))
{
    if (method.IsSpecialName)
        continue; // skip property accessors

    // Only handle methods that can return a string metric name
    if (method.ReturnType.FullName != "System.String")
        continue;

    var parameters = method.GetParameters();
    if (parameters.Length == 0)
    {
        // Parameterless methods not exposed as properties (expression-body methods defined
        // with () syntax instead of =>-property syntax show up as methods, not properties)
        object? val;
        try { val = method.Invoke(null, null); }
        catch { continue; }
        var s = val?.ToString();
        if (IsSupport(s)) discovered.Add(s!);
        continue;
    }

    bool allEnumOrBool = parameters.All(p => p.ParameterType.IsEnum || p.ParameterType.FullName == "System.Boolean");

    if (!allEnumOrBool)
    {
        // Report non-enumerable methods that are likely Supportability-related
        if (method.Name.StartsWith("GetSupportability") || method.Name.StartsWith("Supportability"))
        {
            var sig = $"{method.Name}({string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))})";
            unenumerable.Add(sig);
        }
        continue;
    }

    // Build cross-product of all argument combinations
    var paramValueSets = parameters.Select(p => GetValues(p.ParameterType)).ToArray();
    foreach (var argSet in CrossProduct(paramValueSets))
    {
        object? result;
        try { result = method.Invoke(null, argSet); }
        catch { continue; }

        var str = result?.ToString();
        if (IsSupport(str))
            discovered.Add(str!);
    }
}

// Output discovered names
Console.WriteLine("=== Discovered Supportability metrics ===");
foreach (var name in discovered)
    Console.WriteLine(name);
Console.WriteLine();
Console.WriteLine($"Total: {discovered.Count}");

// Report non-enumerable methods
Console.WriteLine();
Console.WriteLine("=== Methods not enumerated (string/complex params - C/D shape) ===");
foreach (var sig in unenumerable)
    Console.WriteLine($"  {sig}");

// Diff mode
if (diffFile != null)
{
    if (!File.Exists(diffFile))
    {
        Console.Error.WriteLine($"Diff file not found: {diffFile}");
        return 1;
    }

    var anglerLines = File.ReadAllLines(diffFile)
        .Select(l => l.Trim())
        .Where(l => l.StartsWith("Supportability"))
        .ToHashSet(StringComparer.Ordinal);

    Console.WriteLine();
    Console.WriteLine("=== Candidate additions (in code, .NET-named, absent from Angler file) ===");
    bool any = false;
    foreach (var name in discovered)
    {
        if (IsDotNetNamed(name) && !anglerLines.Contains(name) && !exclusions.Contains(name))
        {
            Console.WriteLine($"  + {name}");
            any = true;
        }
    }
    if (!any)
        Console.WriteLine("  (none found)");
}

return 0;

// ---- helpers ----

// Reject obvious prefix leakage (e.g. SupportabilityCachePrefix ends with '/')
static bool IsSupport(string? s) =>
    s != null && s.StartsWith("Supportability", StringComparison.Ordinal) && !s.EndsWith('/');

// Matches names the .NET agent owns by naming convention
static bool IsDotNetNamed(string name) =>
    name.Contains("/DotNet/") || name.Contains("/DotNET/") || name.Contains("/Dotnet/");

static object[] GetValues(Type t)
{
    if (t.FullName == "System.Boolean")
        return new object[] { true, false };
    if (t.IsEnum)
        return Enum.GetValues(t).Cast<object>().ToArray();
    return Array.Empty<object>();
}

static IEnumerable<object[]> CrossProduct(object[][] sets)
{
    if (sets.Length == 0)
    {
        yield return Array.Empty<object>();
        yield break;
    }
    foreach (var first in sets[0])
    {
        foreach (var rest in CrossProduct(sets[1..]))
        {
            var row = new object[1 + rest.Length];
            row[0] = first;
            rest.CopyTo(row, 1);
            yield return row;
        }
    }
}

static string? FindExclusionsFile()
{
    var dir = AppContext.BaseDirectory;
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir, "FullAgent.sln")))
        {
            var candidate = Path.Combine(dir, "build", "MetricNameDiscovery", "exclusions.txt");
            return File.Exists(candidate) ? candidate : null;
        }
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

static HashSet<string> LoadExclusions(string? path)
{
    if (path == null || !File.Exists(path))
        return new HashSet<string>(StringComparer.Ordinal);

    return File.ReadAllLines(path)
        .Select(l => l.Trim())
        .Where(l => l.Length > 0 && !l.StartsWith('#'))
        .ToHashSet(StringComparer.Ordinal);
}

static string? FindAgentHome()
{
    var dir = AppContext.BaseDirectory;
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir, "FullAgent.sln")))
        {
            var candidate = Path.Combine(dir, "src", "Agent", "newrelichome_x64_coreclr");
            return Directory.Exists(candidate) ? candidate : null;
        }
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

class AgentLoadContext(string dir) : AssemblyLoadContext(isCollectible: false)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = Path.Combine(dir, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}
