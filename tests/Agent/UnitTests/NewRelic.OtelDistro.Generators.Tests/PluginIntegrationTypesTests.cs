// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using NewRelic.Agent.Core.OtelDistro;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

/// <summary>
/// Checks the real built plugin assembly (each target framework) against what upstream OTel needs at run time. OTel's
/// native profiler emits each record's integration type name as a plain type reference, and OTel's IntegrationMapper
/// looks up <c>OnMethodBegin</c> / <c>OnMethodEnd</c> by name on that type (static methods, no FlattenHierarchy, so
/// inherited ones are invisible). A name that does not resolve, or a type without those methods, gives no warning at
/// start-up: the customer's app fails on the first call of the instrumented method. This test catches that at build
/// time, for anything between the generator's output and the shipped DLL.
/// </summary>
[TestFixture]
public class PluginIntegrationTypesTests
{
    private const string PluginAssemblyFileName = "NewRelic.OtelDistro.Plugin.dll";
    private const string DefinitionsTypeName = "NewRelic.OtelDistro.Plugin.Generated.GeneratedDistroDefinitions";
    private const string DefinitionsFieldName = "Table";
    private const string IntegrationClassPrefix = "NrDistroIntegration_";
    private const string CallTargetNamespace = "OpenTelemetry.AutoInstrumentation.CallTarget";
    private const string CallTargetStateTypeName = CallTargetNamespace + ".CallTargetState";
    private const string CallTargetReturnTypeName = CallTargetNamespace + ".CallTargetReturn";
    private const string CallTargetReturnOfTTypeName = CallTargetNamespace + ".CallTargetReturn`1";
    private const string OtelAssemblyName = "OpenTelemetry.AutoInstrumentation";
    private const BindingFlags DeclaredStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [TestCase("net8.0")]
    [TestCase("net462")]
    public void PluginIntegrationTypes_EveryRecordResolves(string targetFramework)
    {
        var pluginPath = Path.Combine(BuildPath("PluginOutputDirectory"), targetFramework, PluginAssemblyFileName);
        Assert.That(File.Exists(pluginPath), Is.True, "plugin DLL not built: " + pluginPath);
        var otelDirectory = Path.Combine(BuildPath("OtelAutoInstrumentationPackageDirectory"), "lib", targetFramework);
        Assert.That(File.Exists(Path.Combine(otelDirectory, OtelAssemblyName + ".dll")), Is.True, "OTel auto-instrumentation assembly not found in " + otelDirectory);

        var read = InstrumentationDefinitionsTable.TryRead(ReadTable(pluginPath), out var definitions, out var sites, out _, out var failureReason);
        Assert.That(read, Is.True, "the Core reader rejected the plugin's table: " + failureReason);
        Assert.That(definitions, Is.Not.Empty, "the plugin's table has no records");
        Assert.That(sites.Count, Is.EqualTo(definitions.Count));

        var resolverPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
            .Concat(Directory.GetFiles(Path.GetDirectoryName(pluginPath), "*.dll"))
            .Concat(Directory.GetFiles(otelDirectory, "*.dll"));
        using var context = new MetadataLoadContext(new PathAssemblyResolver(resolverPaths));
        var plugin = context.LoadFromAssemblyPath(pluginPath);

        var problems = new List<string>();
        for (var i = 0; i < definitions.Count; i++)
        {
            var record = $"record {i} ({definitions[i].TargetType}.{definitions[i].TargetMethod} -> {definitions[i].IntegrationTypeName})";
            var type = plugin.GetType(definitions[i].IntegrationTypeName, throwOnError: false);
            if (type == null)
            {
                problems.Add(record + ": integration type does not resolve in the plugin assembly");
                continue;
            }

            problems.AddRange(CheckIntegrationType(type, i, definitions[i].TargetSignatureTypes.Length).Select(p => record + ": " + p));
        }

        var recordsByTypeName = definitions.GroupBy(d => d.IntegrationTypeName).ToDictionary(g => g.Key, g => g.Count());
        foreach (var type in plugin.GetTypes().Where(t => t.Name.StartsWith(IntegrationClassPrefix, StringComparison.Ordinal)))
        {
            recordsByTypeName.TryGetValue(type.FullName, out var count);
            if (count != 1)
            {
                problems.Add($"{type.FullName}: named by {count} records, expected exactly 1");
            }
        }

        Assert.That(problems, Is.Empty, $"{targetFramework} plugin, {definitions.Count} records:{Environment.NewLine}" + string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// The generated OnMethodEnd calls DistroCallTarget.End: a generic End&lt;TReturn&gt; (so a value-type return is boxed only
    /// when a tracer started) and a void End(in state, exception). No End taking an object return value may exist, so
    /// nothing can bind to a boxing overload.
    /// </summary>
    [TestCase("net8.0")]
    [TestCase("net462")]
    public void PluginCallTarget_EndIsGenericAndVoidOnly_NoObjectOverload(string targetFramework)
    {
        var pluginPath = Path.Combine(BuildPath("PluginOutputDirectory"), targetFramework, PluginAssemblyFileName);
        Assert.That(File.Exists(pluginPath), Is.True, "plugin DLL not built: " + pluginPath);
        var otelDirectory = Path.Combine(BuildPath("OtelAutoInstrumentationPackageDirectory"), "lib", targetFramework);
        var resolverPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
            .Concat(Directory.GetFiles(Path.GetDirectoryName(pluginPath), "*.dll"))
            .Concat(Directory.GetFiles(otelDirectory, "*.dll"));
        using var context = new MetadataLoadContext(new PathAssemblyResolver(resolverPaths));
        var callTarget = context.LoadFromAssemblyPath(pluginPath).GetType("NewRelic.OtelDistro.Plugin.DistroCallTarget", throwOnError: true);

        var ends = callTarget.GetMethods(DeclaredStatic).Where(m => m.Name == "End").Select(m => m.ToString()).ToList();

        Assert.That(ends, Is.EquivalentTo(new[]
        {
            "System.Void End[TReturn](OpenTelemetry.AutoInstrumentation.CallTarget.CallTargetState&, TReturn, System.Exception)",
            "System.Void End(OpenTelemetry.AutoInstrumentation.CallTarget.CallTargetState&, System.Exception)",
        }));
    }

    /// <summary>
    /// The csproj feeds every shipped wrapper XML plus Misc to the generator twice: once as a compiled AdditionalFiles
    /// input (the home-directory copy, condition on TargetFramework -- only the XML files that TFM's home actually
    /// ships, since the .NET Core and .NET Framework homes carry different wrapper sets) and once hash-only
    /// (NrDistroHashOnly metadata, made visible to the generator by CompilerVisibleItemMetadata, covering every
    /// wrapper folder regardless of which home ships it). This checks that wiring in the real build: one manifest
    /// file per wrapper folder plus Misc, exactly this TFM's home-shipped files compiled, and the Sql DataReader
    /// points default-off.
    /// </summary>
    [TestCase("net8.0")]
    [TestCase("net462")]
    public void PluginTable_ManifestListsEveryShippedFileAllCompiled(string targetFramework)
    {
        var pluginPath = Path.Combine(BuildPath("PluginOutputDirectory"), targetFramework, PluginAssemblyFileName);
        Assert.That(File.Exists(pluginPath), Is.True, "plugin DLL not built: " + pluginPath);
        var read = InstrumentationDefinitionsTable.TryRead(ReadTable(pluginPath), out _, out var sites, out var manifest, out var failureReason);
        Assert.That(read, Is.True, "the Core reader rejected the plugin's table: " + failureReason);

        var wrapperDirectory = Path.Combine(BuildPath("RepositoryRootDirectory"), "src", "Agent", "NewRelic", "Agent", "Extensions", "Providers", "Wrapper");
        var expected = Directory.GetDirectories(wrapperDirectory)
            .Where(d => File.Exists(Path.Combine(d, "Instrumentation.xml")))
            .Select(d => "NewRelic.Providers.Wrapper." + Path.GetFileName(d) + ".Instrumentation.xml")
            .Append("NewRelic.Providers.Wrapper.Misc.Instrumentation.xml")
            .ToList();

        // Mirrors the plugin csproj's per-TFM AdditionalFiles condition: net8.0 reads the .NET Core x64 home,
        // net462 the .NET Framework x64 home. Only the XML files that TFM's home actually ships get compiled.
        var homeDirectoryName = targetFramework == "net8.0" ? "newrelichome_x64_coreclr" : "newrelichome_x64";
        var homeExtensionsDirectory = Path.Combine(BuildPath("RepositoryRootDirectory"), "src", "Agent", homeDirectoryName, "extensions");
        Assert.That(Directory.Exists(homeExtensionsDirectory), Is.True, "agent home not built: " + homeExtensionsDirectory);
        var expectedCompiled = Directory.GetFiles(homeExtensionsDirectory, "*.xml").Select(Path.GetFileName).ToList();

        var names = Enumerable.Range(0, manifest.FileCount).Select(manifest.HomeName).ToList();
        var compiled = Enumerable.Range(0, manifest.FileCount).Where(manifest.IsCompiled).Select(manifest.HomeName).ToList();
        var defaultOffFactories = Enumerable.Range(0, sites.Count).Where(sites.IsDefaultOff).Select(i => sites.TryGet(i, out var site) ? site.TracerFactoryName : null).Distinct().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(names, Is.EquivalentTo(expected));
            Assert.That(compiled, Is.EquivalentTo(expectedCompiled));
            Assert.That(defaultOffFactories, Is.EquivalentTo(new[] { "DataReaderTracer", "DataReaderTracerAsync" }));
        });
    }

    /// <summary>
    /// <c>DistroXmlEffectiveSet.Compute</c>'s no-change fast path returns the per-site defaults, while the full computation
    /// decides per point key. They agree only when every record sharing a point key also shares its default-off flag and
    /// its source file. Pins that invariant on the real shipped table, so a generator change that breaks it fails here
    /// instead of silently differing at start-up.
    /// </summary>
    [TestCase("net8.0")]
    [TestCase("net462")]
    public void PluginTable_RecordsSharingAPointKeyShareDefaultOffAndSourceFile(string targetFramework)
    {
        var pluginPath = Path.Combine(BuildPath("PluginOutputDirectory"), targetFramework, PluginAssemblyFileName);
        Assert.That(File.Exists(pluginPath), Is.True, "plugin DLL not built: " + pluginPath);
        var read = InstrumentationDefinitionsTable.TryRead(ReadTable(pluginPath), out _, out var sites, out _, out var failureReason);
        Assert.That(read, Is.True, "the Core reader rejected the plugin's table: " + failureReason);
        Assert.That(sites.Count, Is.GreaterThan(0));

        var violations = Enumerable.Range(0, sites.Count)
            .GroupBy(i => sites.PointKey(i))
            .Where(g => g.Select(sites.IsDefaultOff).Distinct().Count() > 1 || g.Select(sites.SourceFile).Distinct().Count() > 1)
            .Select(g => $"point key {g.Key}: records {string.Join(", ", g)}")
            .ToList();

        Assert.That(violations, Is.Empty, "records sharing a point key differ in default-off or source file:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>The shape IntegrationMapper accepts, checked on the type's own (declared) members.</summary>
    private static IEnumerable<string> CheckIntegrationType(Type type, int recordIndex, int targetSignatureLength)
    {
        var begins = type.GetMethods(DeclaredStatic).Where(m => m.Name == "OnMethodBegin").ToList();
        if (begins.Count != 1)
        {
            yield return $"declares {begins.Count} static OnMethodBegin methods, expected exactly 1";
        }
        else if (!begins[0].IsPublic || !IsCallTargetType(begins[0].ReturnType, CallTargetStateTypeName) || begins[0].GetGenericArguments().Length < 1)
        {
            yield return "OnMethodBegin is not a public static generic method returning CallTargetState: " + begins[0];
        }
        else if (begins[0].GetParameters().Length != targetSignatureLength)
        {
            // Instance parameter + N arguments == "_" + N parameter types.
            yield return $"OnMethodBegin takes {begins[0].GetParameters().Length} parameters, expected {targetSignatureLength} (the record's TargetSignatureTypes length): " + begins[0];
        }

        var ends = type.GetMethods(DeclaredStatic).Where(m => m.Name == "OnMethodEnd").ToList();
        if (ends.Count != 2 || ends.Count(IsVoidOnMethodEnd) != 1 || ends.Count(IsNonVoidOnMethodEnd) != 1)
        {
            yield return "OnMethodEnd methods are not exactly the void and non-void shapes: " + string.Join("; ", ends);
        }

        var siteIndex = type.GetField("SiteIndex", DeclaredStatic);
        if (siteIndex == null || !siteIndex.IsLiteral || !(siteIndex.GetRawConstantValue() is int value))
        {
            yield return "declares no int SiteIndex constant";
        }
        else if (value != recordIndex)
        {
            yield return $"SiteIndex is {value}, expected {recordIndex}";
        }
    }

    /// <summary><c>CallTargetReturn OnMethodEnd&lt;TTarget&gt;(TTarget, Exception, in CallTargetState)</c>.</summary>
    private static bool IsVoidOnMethodEnd(MethodInfo method)
    {
        if (!method.IsPublic || !method.IsGenericMethodDefinition)
        {
            return false;
        }

        var generics = method.GetGenericArguments();
        var parameters = method.GetParameters();
        return generics.Length == 1
            && IsCallTargetType(method.ReturnType, CallTargetReturnTypeName)
            && parameters.Length == 3
            && parameters[0].ParameterType == generics[0]
            && parameters[1].ParameterType.FullName == "System.Exception"
            && IsInCallTargetState(parameters[2]);
    }

    /// <summary><c>CallTargetReturn&lt;TReturn&gt; OnMethodEnd&lt;TTarget, TReturn&gt;(TTarget, TReturn, Exception, in CallTargetState)</c>.</summary>
    private static bool IsNonVoidOnMethodEnd(MethodInfo method)
    {
        if (!method.IsPublic || !method.IsGenericMethodDefinition)
        {
            return false;
        }

        var generics = method.GetGenericArguments();
        var parameters = method.GetParameters();
        return generics.Length == 2
            && method.ReturnType.IsGenericType
            && IsCallTargetType(method.ReturnType.GetGenericTypeDefinition(), CallTargetReturnOfTTypeName)
            && method.ReturnType.GetGenericArguments().Single() == generics[1]
            && parameters.Length == 4
            && parameters[0].ParameterType == generics[0]
            && parameters[1].ParameterType == generics[1]
            && parameters[2].ParameterType.FullName == "System.Exception"
            && IsInCallTargetState(parameters[3]);
    }

    // Deliberately stricter than OTel's IntegrationMapper, which also accepts the state by value: our emitter always emits `in`.
    private static bool IsInCallTargetState(ParameterInfo parameter) =>
        parameter.ParameterType.IsByRef && parameter.IsIn && IsCallTargetType(parameter.ParameterType.GetElementType(), CallTargetStateTypeName);

    private static bool IsCallTargetType(Type type, string fullName) =>
        type.FullName == fullName && type.Assembly.GetName().Name == OtelAssemblyName;

    /// <summary>
    /// <c>GeneratedDistroDefinitions.Table</c>, read by loading the plugin into a collectible load context and reading the
    /// static field (its initializer is a plain array copy, so this works for the net462 build too).
    /// </summary>
    private static byte[] ReadTable(string pluginPath)
    {
        var loadContext = new AssemblyLoadContext("PluginIntegrationTypesTests", isCollectible: true);
        try
        {
            var definitionsType = loadContext.LoadFromAssemblyPath(pluginPath).GetType(DefinitionsTypeName, throwOnError: true);
            var field = definitionsType.GetField(DefinitionsFieldName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, DefinitionsTypeName + "." + DefinitionsFieldName + " not found");
            return (byte[])((byte[])field.GetValue(null)).Clone();
        }
        finally
        {
            loadContext.Unload();
        }
    }

    /// <summary>A path the test project's build baked into this assembly (see the csproj's AssemblyMetadata items).</summary>
    private static string BuildPath(string key)
    {
        var value = typeof(PluginIntegrationTypesTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(a => a.Key == key)?.Value;
        Assert.That(value, Is.Not.Null.And.Not.Empty, "build did not record " + key);
        return value;
    }
}
