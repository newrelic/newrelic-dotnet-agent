// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Coordinator.Tests;

/// <summary>
/// Compares the coordinator's private copy of OTel's native definition struct, and its P/Invoke declaration, with the
/// ones in the pinned OTel package (read through MetadataLoadContext, so nothing from OTel is loaded). OTel gives no
/// compile-time link to these: a layout change would make the coordinator hand garbage to the OTel native profiler.
/// Failing here, at build time, is the point; the failure messages say what to update.
/// </summary>
[TestFixture]
public class OtelNativeLayoutCompatibilityTests
{
    private const string OtelAssemblyFileName = "OpenTelemetry.AutoInstrumentation.dll";
    private const string DefinitionTypeName = "OpenTelemetry.AutoInstrumentation.NativeCallTargetDefinition2";
    private const string NativeMethodsTypeName = "OpenTelemetry.AutoInstrumentation.NativeMethods";
    private const string RegisterMethodName = "RegisterCallTargetDefinitions";
    private const string RegistrarSourcePath = "src/Agent/NewRelic/OtelDistro/NewRelic.OtelDistro.Coordinator/OtelNativeRegistrar.cs";

#if NETFRAMEWORK
    private const string OtelTargetFramework = "net462";
#else
    private const string OtelTargetFramework = "net8.0";
#endif

    [Test]
    public void Coordinator_Matches_Otel_NativeCallTargetDefinition2()
    {
        using var otel = OtelAssembly.Load();
        var type = otel.Assembly.GetType(DefinitionTypeName, throwOnError: false);
        if (type == null)
        {
            Assert.Fail($"OTel {otel.Version}: {DefinitionTypeName} not found; the coordinator's native struct copy must be re-derived.");
        }

        var otelSignature = DefinitionLayout.Describe(type);

        Assert.That(OtelNativeRegistrar.LayoutSignature, Is.EqualTo(otelSignature),
            $"OTel {otel.Version} changed NativeCallTargetDefinition2: {DefinitionLayout.FirstDifference(OtelNativeRegistrar.LayoutSignature, otelSignature)}. " +
            $"Update OtelNativeRegistrar.NativeDefinition ({RegistrarSourcePath}) to match, then re-run the HWC coordinator smoke (MultiAppDomainDistroTests).");
    }

    [Test]
    public void Coordinator_Matches_Otel_RegisterCallTargetDefinitions()
    {
        using var otel = OtelAssembly.Load();
        var windowsMethods = otel.Assembly.GetType(NativeMethodsTypeName, throwOnError: false)
            ?.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(t => t.Name == "Windows")
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.Name == RegisterMethodName)
            .ToList();
        if (windowsMethods == null || windowsMethods.Count != 1)
        {
            Assert.Fail($"OTel {otel.Version}: {NativeMethodsTypeName}+Windows.{RegisterMethodName} not found (or ambiguous); the coordinator's DllImport must be re-derived.");
        }

        var otelMethod = windowsMethods.Single();
        var ours = typeof(OtelNativeRegistrar).GetMethod(RegisterMethodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(ours, Is.Not.Null, "OtelNativeRegistrar no longer declares " + RegisterMethodName);

        var otelImport = DllImportInfo.Read(otelMethod);
        var ourImport = DllImportInfo.Read(ours);
        var differences = new List<string>();
        if (OtelNativeRegistrar.RegisterSignature != DefinitionLayout.DescribeMethod(otelMethod))
        {
            differences.Add(DefinitionLayout.FirstDifference(OtelNativeRegistrar.RegisterSignature, DefinitionLayout.DescribeMethod(otelMethod)));
        }

        if (ourImport.DllName != otelImport.DllName)
        {
            differences.Add($"DllImport library: expected {ourImport.DllName}, found {otelImport.DllName}");
        }

        if (ourImport.IdStringEncoding != otelImport.IdStringEncoding)
        {
            differences.Add($"id string marshalling: expected {ourImport.IdStringEncoding}, found {otelImport.IdStringEncoding}");
        }

        Assert.That(differences, Is.Empty,
            $"OTel {otel.Version} changed {RegisterMethodName}: {string.Join("; ", differences)}. Update OtelNativeRegistrar's DllImport (same file) to match.");
    }

    /// <summary>
    /// What a <c>[DllImport]</c> declares that the coordinator must agree on: the library, and how the leading string
    /// parameter (the registration id) is marshalled. OTel marks that parameter <c>LPWStr</c> and leaves the DllImport
    /// charset at its default; the coordinator sets <c>CharSet.Unicode</c> instead. Both mean UTF-16, so the comparison
    /// is on that effective encoding rather than on the two attributes separately.
    /// </summary>
    private sealed class DllImportInfo
    {
        private DllImportInfo(string dllName, string idStringEncoding)
        {
            DllName = dllName;
            IdStringEncoding = idStringEncoding;
        }

        public string DllName { get; }

        public string IdStringEncoding { get; }

        public static DllImportInfo Read(MethodInfo method)
        {
            var dllImport = method.GetCustomAttributesData().SingleOrDefault(a => a.AttributeType.FullName == typeof(DllImportAttribute).FullName);
            Assert.That(dllImport, Is.Not.Null, $"{method.DeclaringType.FullName}.{method.Name} has no [DllImport] visible to reflection");

            var charSet = dllImport.NamedArguments.Where(a => a.MemberName == nameof(DllImportAttribute.CharSet)).Select(a => (CharSet?)Convert.ToInt32(a.TypedValue.Value)).SingleOrDefault();
            var idParameter = method.GetParameters()[0];
            var marshalAs = idParameter.GetCustomAttributesData().SingleOrDefault(a => a.AttributeType.FullName == typeof(MarshalAsAttribute).FullName);
            var encoding = marshalAs != null
                ? ((UnmanagedType)Convert.ToInt32(marshalAs.ConstructorArguments[0].Value)).ToString()
                : "default:" + (CharSet)(charSet ?? CharSet.Ansi);

            return new DllImportInfo((string)dllImport.ConstructorArguments[0].Value, NormalizeEncoding(encoding));
        }

        private static string NormalizeEncoding(string encoding) => encoding == "LPWStr" || encoding == "default:Unicode" ? "Unicode" : encoding;
    }

    private sealed class OtelAssembly : IDisposable
    {
        private readonly MetadataLoadContext _context;

        private OtelAssembly(MetadataLoadContext context, Assembly assembly, string version)
        {
            _context = context;
            Assembly = assembly;
            Version = version;
        }

        public Assembly Assembly { get; }

        // The package version (the folder name): the OTel assembly's own AssemblyVersion is a constant 1.0.0.0.
        public string Version { get; }

        public static OtelAssembly Load()
        {
            var packageDirectory = typeof(OtelNativeLayoutCompatibilityTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .SingleOrDefault(a => a.Key == "OtelAutoInstrumentationPackageDirectory")?.Value;
            Assert.That(packageDirectory, Is.Not.Null.And.Not.Empty, "build did not record OtelAutoInstrumentationPackageDirectory");

            var otelDirectory = Path.Combine(packageDirectory, "lib", OtelTargetFramework);
            var otelPath = Path.Combine(otelDirectory, OtelAssemblyFileName);
            Assert.That(File.Exists(otelPath), Is.True, "OTel auto-instrumentation assembly not found: " + otelPath);

            var resolverPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll").Concat(Directory.GetFiles(otelDirectory, "*.dll"));
            var context = new MetadataLoadContext(new PathAssemblyResolver(resolverPaths));
            return new OtelAssembly(context, context.LoadFromAssemblyPath(otelPath), Path.GetFileName(packageDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        }

        public void Dispose() => _context.Dispose();
    }
}
