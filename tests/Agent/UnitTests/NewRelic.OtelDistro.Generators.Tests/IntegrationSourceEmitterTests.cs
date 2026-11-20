// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class IntegrationSourceEmitterTests
{
    private const string Namespace = "NewRelic.OtelDistro.Plugin.Generated";

    [Test]
    public void Emit_OnePoint_ContainsIntegrationClassAndDefinition()
    {
        var point = new XmlInstrumentationPoint(
            "NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper",
            "Custom/Step0",
            "Step0App",
            "Step0App.Worker",
            "DoWork",
            new[] { "System.Int32" });

        var functionId = FunctionIdHasher.Compute("Step0App", "Step0App.Worker", "DoWork", "System.Int32");
        var className = "NrDistroIntegration_" + functionId.ToString("X16", CultureInfo.InvariantCulture);

        var source = EmitOrFail(point);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain($"public abstract class {className}"));
            Assert.That(source, Does.Contain("    public const int SiteIndex = 0;"));
            Assert.That(source, Does.Contain($"=> DistroCallTarget.Begin<{className}, TTarget, TArg1>(SiteIndex, instance, arg1);"));
            Assert.That(source, Does.Contain("public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)"));
            Assert.That(source, Does.Contain("public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)"));
            // The generic End is named explicitly (no object overload to fall back to), so a value-type return is not boxed
            // before End sees an empty state.
            Assert.That(source, Does.Contain("        DistroCallTarget.End<TReturn>(in state, returnValue, exception);"));
            Assert.That(source, Does.Contain("        DistroCallTarget.End(in state, exception);"));
            Assert.That(source, Does.Not.Contain("DistroCallTarget.End(in state, null, exception);"));
            Assert.That(GeneratedTable.ReadDefinitions(source).Select(GeneratedTable.Describe), Is.EqualTo(new[]
            {
                $"Step0App | Step0App.Worker | DoWork | [_,System.Int32] | {Namespace}.{className} | 0.0.0-65535.65535.65535"
            }));
        });
    }

    [Test]
    public void Emit_ZeroParameters_EmitsBeginWithoutArgs()
    {
        var point = new XmlInstrumentationPoint("Factory", null, "A", "B", "M", new List<string>());

        var source = EmitOrFail(point);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)"));
            Assert.That(source, Does.Contain($"=> DistroCallTarget.Begin<{ClassNameFor("A", "B", "M", "")}, TTarget>(SiteIndex, instance);"));
        });
    }

    [Test]
    public void Emit_OnMethodBegin_CallsGenericBeginWithOwnClass()
    {
        var oneParameter = EmitOrFail(new XmlInstrumentationPoint("Factory", null, "A", "B", "One", new[] { "System.Int32" }));
        var eightTypes = new[] { "System.Int32", "System.String", "System.Int64", "System.Boolean", "System.Byte", "System.Char", "System.Double", "System.Object" };
        var eightParameters = EmitOrFail(new XmlInstrumentationPoint("Factory", null, "A", "B", "Eight", eightTypes));
        var oneClass = ClassNameFor("A", "B", "One", "System.Int32");
        var eightClass = ClassNameFor("A", "B", "Eight", string.Join(",", eightTypes));

        Assert.Multiple(() =>
        {
            Assert.That(oneParameter, Does.Contain("public static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 arg1)"));
            Assert.That(oneParameter, Does.Contain($"        => DistroCallTarget.Begin<{oneClass}, TTarget, TArg1>(SiteIndex, instance, arg1);"));
            Assert.That(eightParameters, Does.Contain("public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(TTarget instance, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4, ref TArg5 arg5, ref TArg6 arg6, ref TArg7 arg7, ref TArg8 arg8)"));
            Assert.That(eightParameters, Does.Contain($"        => DistroCallTarget.Begin<{eightClass}, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(SiteIndex, instance, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);"));
            Assert.That(eightParameters, Does.Not.Contain("new object[]"));
        });
    }

    private static string ClassNameFor(string assembly, string type, string method, string argumentSignature) =>
        "NrDistroIntegration_" + FunctionIdHasher.Compute(assembly, type, method, argumentSignature).ToString("X16", CultureInfo.InvariantCulture);

    [Test]
    public void Emit_ClassHasSiteIndexEqualToRecordIndex()
    {
        // Entries are emitted Zed, Abc, Zed-wide; the writer's sort by assembly moves Abc's record to index 0, so emit
        // order and record order differ and each class must take its index from where its record ended up.
        var points = new[]
        {
            new XmlInstrumentationPoint("FactoryZ", "Custom/Z", "Zed", "T", "Explicit", new[] { "System.Int32" }),
            new XmlInstrumentationPoint("FactoryA", null, "Abc", "T", "Explicit", new List<string>()),
            new XmlInstrumentationPoint("FactoryW", null, "Zed", "T", "Wide", null),
        };

        var source = EmitOrFail(points);
        var (definitions, sites) = GeneratedTable.Read(source);
        var siteIndexByType = GeneratedTable.SiteIndexByIntegrationType(source, Namespace);

        Assert.Multiple(() =>
        {
            Assert.That(siteIndexByType.Keys, Is.EquivalentTo(definitions.Select(d => d.IntegrationTypeName)));
            for (var i = 0; i < definitions.Count; i++)
            {
                Assert.That(siteIndexByType[definitions[i].IntegrationTypeName], Is.EqualTo(i), definitions[i].IntegrationTypeName);

                // The site at that index is the class's own target, not a neighbour's.
                Assert.That(sites[i].AssemblyName + "." + sites[i].MethodName, Is.EqualTo(definitions[i].TargetAssembly + "." + definitions[i].TargetMethod));
            }

            Assert.That(siteIndexByType[Namespace + ".NrDistroIntegration_" + FunctionIdHasher.Compute("Abc", "T", "Explicit", "").ToString("X16", CultureInfo.InvariantCulture)], Is.EqualTo(0));
            Assert.That(siteIndexByType[Namespace + ".NrDistroIntegration_" + FunctionIdHasher.Compute("Zed", "T", "Explicit", "System.Int32").ToString("X16", CultureInfo.InvariantCulture)], Is.EqualTo(1));
        });
    }

    [Test]
    public void Emit_ClassHasNoStaticSiteField()
    {
        var source = EmitOrFail(
            new XmlInstrumentationPoint("Factory", "Custom/M", "A", "B", "M", new[] { "System.Int32" }),
            new XmlInstrumentationPoint("Factory", null, "A", "B", "Wide", null));

        var classSource = source.Substring(0, source.IndexOf("public static class GeneratedDistroDefinitions", StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(classSource, Does.Contain("public abstract class NrDistroIntegration_"));
            Assert.That(classSource, Does.Not.Contain("DistroInstrumentationSite"));
            Assert.That(classSource, Does.Not.Contain("static readonly"));
            Assert.That(classSource, Does.Not.Contain("static NrDistroIntegration_"));
            Assert.That(classSource, Does.Not.Contain("\"Factory\""));
            Assert.That(classSource, Does.Not.Contain("Custom/M"));
            Assert.That(source, Does.Not.Contain("new DistroInstrumentationSite"));
        });
    }

    [Test]
    public void Emit_ClassCommentNamesTarget()
    {
        var source = EmitOrFail(
            new XmlInstrumentationPoint("Factory", null, "Asm", "NS.Type", "Explicit", new[] { "System.Int32", "System.String" }),
            new XmlInstrumentationPoint("Factory", null, "Asm", "NS.Type", "None", new List<string>()),
            new XmlInstrumentationPoint("Factory", null, "Asm", "NS.Type", "Wide", null));

        var explicitClass = "NrDistroIntegration_" + FunctionIdHasher.Compute("Asm", "NS.Type", "Explicit", "System.Int32,System.String").ToString("X16", CultureInfo.InvariantCulture);
        var noneClass = "NrDistroIntegration_" + FunctionIdHasher.Compute("Asm", "NS.Type", "None", "").ToString("X16", CultureInfo.InvariantCulture);
        var wideClass = "NrDistroIntegration_" + FunctionIdHasher.Compute("Asm", "NS.Type", "Wide", "*2").ToString("X16", CultureInfo.InvariantCulture);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain($"// Asm / NS.Type.Explicit(System.Int32,System.String){Environment.NewLine}public abstract class {explicitClass}"));
            Assert.That(source, Does.Contain($"// Asm / NS.Type.None(){Environment.NewLine}public abstract class {noneClass}"));
            Assert.That(source, Does.Contain($"// Asm / NS.Type.Wide(any 2 parameters){Environment.NewLine}public abstract class {wideClass}"));
        });
    }

    [Test]
    public void Emit_ClassCommentWithLineBreaksInNames_StaysOneLine()
    {
        // XML attribute values can carry line breaks (&#10;); one in the comment would end it and break the build.
        var source = EmitOrFail(new XmlInstrumentationPoint("Factory", null, "A\r\nB", "C\nD" + (char)0x2028 + "E", "M\u0085", new List<string>()));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("// A  B / C D E.M ()" + Environment.NewLine + "public abstract class NrDistroIntegration_"));
            Assert.That(GeneratedTable.ReadDefinitions(source).Single().TargetAssembly, Is.EqualTo("A\r\nB"));
        });
    }

    [Test]
    public void Emit_NoParameters_EmitsNineArities()
    {
        var point = new XmlInstrumentationPoint("Factory", null, "A", "B", "M", null);

        var source = EmitOrFail(point);

        var ids = new HashSet<string>();
        var expectedDefinitions = new List<string>();

        Assert.Multiple(() =>
        {
            for (var arity = 0; arity <= IntegrationSourceEmitter.MaxParameters; arity++)
            {
                var functionId = FunctionIdHasher.Compute("A", "B", "M", "*" + arity);
                var className = "NrDistroIntegration_" + functionId.ToString("X16", CultureInfo.InvariantCulture);
                ids.Add(className);

                Assert.That(source, Does.Contain($"public abstract class {className}"));
                Assert.That(source, Does.Contain($"// A / B.M(any {arity} parameters){Environment.NewLine}public abstract class {className}{Environment.NewLine}{{{Environment.NewLine}    public const int SiteIndex = {arity};"));

                var expectedUnderscores = string.Join(",", Enumerable.Repeat("_", arity + 1));
                expectedDefinitions.Add($"A | B | M | [{expectedUnderscores}] | {Namespace}.{className} | 0.0.0-65535.65535.65535");
            }

            Assert.That(GeneratedTable.ReadDefinitions(source).Select(GeneratedTable.Describe), Is.EqualTo(expectedDefinitions));
            Assert.That(ids, Has.Count.EqualTo(9));
            Assert.That(CountOccurrences(source, "public abstract class NrDistroIntegration_"), Is.EqualTo(9));
        });
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    [Test]
    public void Emit_PointWithTooManyParameters_IsSkipped()
    {
        var parameters = new List<string>();
        for (var i = 0; i < IntegrationSourceEmitter.MaxParameters + 1; i++)
        {
            parameters.Add("System.Int32");
        }

        var point = new XmlInstrumentationPoint("Factory", null, "A", "B", "M", parameters);

        var source = EmitOrFail(point);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Contain("NrDistroIntegration_"));
            Assert.That(GeneratedTable.ReadDefinitions(source), Is.Empty);
        });
    }

    [Test]
    public void Emit_ExplicitAndOverloadWidePoints_TableIsSortedByAssemblyAndKeepsTheirOrder()
    {
        var points = new[]
        {
            new XmlInstrumentationPoint("Factory", null, "Zed", "T", "Explicit", new[] { "System.Int32" }),
            new XmlInstrumentationPoint("Factory", null, "Abc", "T", "Explicit", new List<string>()),
            new XmlInstrumentationPoint("Factory", null, "Zed", "T", "Wide", null),
        };

        var definitions = GeneratedTable.ReadDefinitions(EmitOrFail(points));

        // Abc first; within Zed the explicit match stays ahead of the nine overload-wide arity variants.
        Assert.That(definitions.Select(d => d.TargetAssembly + "." + d.TargetMethod + "/" + d.TargetSignatureTypes.Length), Is.EqualTo(
            new[] { "Abc.Explicit/1", "Zed.Explicit/2" }.Concat(Enumerable.Range(1, 9).Select(n => "Zed.Wide/" + n))));
    }

    [Test]
    public void Emit_Table_HasCommentHeaderAndSixteenBytesPerLine()
    {
        var source = EmitOrFail(new XmlInstrumentationPoint("Factory", null, "Asm", "Type", "Method", new[] { "System.Int32" }));

        var tableLines = source.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .SkipWhile(line => !line.Contains("Table = new byte[]"))
            .Skip(2)
            .TakeWhile(line => line.Trim() != "};")
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("NRIDT instrumentation definitions table, format version 3: 7 strings, 1 signatures, 1 records, 1 assemblies, 0 files"));
            Assert.That(source, Does.Contain("src/Agent/NewRelic/Agent/Core/OtelDistro/InstrumentationDefinitionsTableFormat.cs"));
            Assert.That(tableLines.Take(tableLines.Count - 1).Select(CountBytes), Is.All.EqualTo(16));
            Assert.That(CountBytes(tableLines.Last()), Is.InRange(1, 16));
        });
    }

    [Test]
    public void Emit_TableOverALimit_ReturnsFalseWithTheReasonAndNoSource()
    {
        var point = new XmlInstrumentationPoint("Factory", null, "Asm", "Type", new string('M', 65536), new[] { "System.Int32" });

        var emitted = IntegrationSourceEmitter.TryEmit(new[] { point }, Namespace, out var source, out var failureReason);

        Assert.Multiple(() =>
        {
            Assert.That(emitted, Is.False);
            Assert.That(source, Is.Null);
            Assert.That(failureReason, Does.Contain("65536 UTF-8 bytes"));
        });
    }

    [Test]
    public void Emit_RangedExplicitPoint_EntryCarriesConvertedBoundsAndRangedFunctionId()
    {
        var point = new XmlInstrumentationPoint("Factory", null, "A", "B", "M", new[] { "System.Int32" }, "1.0.0.0", "6.0.0.0");
        var functionId = FunctionIdHasher.Compute("A", "B", "M", "System.Int32", "1.0.0-5.65535.65535");
        var className = "NrDistroIntegration_" + functionId.ToString("X16", CultureInfo.InvariantCulture);

        var (definitions, sites) = GeneratedTable.Read(EmitOrFail(point));

        Assert.Multiple(() =>
        {
            Assert.That(definitions.Select(GeneratedTable.Describe), Is.EqualTo(new[]
            {
                $"A | B | M | [_,System.Int32] | {Namespace}.{className} | 1.0.0-5.65535.65535"
            }));
            Assert.That(sites.Single().FunctionId, Is.EqualTo(functionId));
        });
    }

    [Test]
    public void Emit_RangedOverloadWidePoint_EveryArityCarriesTheBoundsAndARangedFunctionId()
    {
        var point = new XmlInstrumentationPoint("Factory", null, "A", "B", "M", null, "6.0.0.0", null);

        var definitions = GeneratedTable.ReadDefinitions(EmitOrFail(point));

        var expected = Enumerable.Range(0, IntegrationSourceEmitter.MaxParameters + 1).Select(arity =>
            $"A | B | M | [{string.Join(",", Enumerable.Repeat("_", arity + 1))}] | {Namespace}.NrDistroIntegration_"
            + FunctionIdHasher.Compute("A", "B", "M", "*" + arity, "6.0.0-65535.65535.65535").ToString("X16", CultureInfo.InvariantCulture)
            + " | 6.0.0-65535.65535.65535");
        Assert.That(definitions.Select(GeneratedTable.Describe), Is.EqualTo(expected));
    }

    [Test]
    public void Emit_UnrangedPoint_KeepsTheDefaultBoundsAndPreRangeFunctionId()
    {
        var (definitions, sites) = GeneratedTable.Read(EmitOrFail(new XmlInstrumentationPoint("Factory", null, "Step0App", "Step0App.Worker", "DoWork", new[] { "System.Int32" })));

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.Describe(definitions.Single()), Does.EndWith(" | 0.0.0-65535.65535.65535"));
            // Hard-coded pre-change value (FunctionIdHasherTests.Step0ExpectedHash): unranged ids must not move.
            Assert.That(sites.Single().FunctionId, Is.EqualTo(0xD43654B7FA81DBC4UL));
        });
    }

    [Test]
    public void Emit_PointsOfMappedAssemblies_EmitsTheSortedDistinctDisableAndExcludeArrays()
    {
        var source = EmitOrFail(
            new XmlInstrumentationPoint("Factory", null, "Npgsql", "T", "M", new[] { "System.Int32" }),
            new XmlInstrumentationPoint("Factory", null, "MySql.Data", "T", "M", new[] { "System.Int32" }),
            new XmlInstrumentationPoint("Factory", null, "Microsoft.Data.SqlClient", "T", "M", new[] { "System.Int32" }),
            new XmlInstrumentationPoint("Factory", null, "Hangfire.Core", "T", "M", new[] { "System.Int32" }));

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.EqualTo(new[] { "ADONET", "MYSQLDATA", "NPGSQL", "SQLCLIENT" }));
            Assert.That(GeneratedTable.ReadStringArray(source, "ExcludedActivitySourceAdditions"), Is.EqualTo(new[] { "OpenTelemetry.AutoInstrumentation.AdoNet", "OpenTelemetry.Instrumentation.SqlClient" }));
            Assert.That(source, Does.Contain("public static readonly string[] DisabledOtelTraceInstrumentationIds"));
            Assert.That(source, Does.Contain("public static readonly string[] ExcludedActivitySourceAdditions"));
            Assert.That(source, Does.Contain("OTel 1.18.0"));
        });
    }

    // A point dropped for too many parameters gives NR nothing there, so OTel must keep instrumenting that library.
    [Test]
    public void Emit_MappedAssemblyWhoseOnlyPointIsDropped_DisablesNothing()
    {
        var nineTypes = Enumerable.Repeat("System.Int32", IntegrationSourceEmitter.MaxParameters + 1).ToArray();

        var source = EmitOrFail(new XmlInstrumentationPoint("Factory", null, "Npgsql", "T", "M", nineTypes));

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadDefinitions(source), Is.Empty);
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.Empty);
            Assert.That(GeneratedTable.ReadStringArray(source, "ExcludedActivitySourceAdditions"), Is.Empty);
        });
    }

    [Test]
    public void Emit_DroppedAndEmittedPoints_DisablesOnlyForTheEmittedOnes()
    {
        var nineTypes = Enumerable.Repeat("System.Int32", IntegrationSourceEmitter.MaxParameters + 1).ToArray();

        var source = EmitOrFail(
            new XmlInstrumentationPoint("Factory", null, "Npgsql", "T", "M", nineTypes),
            new XmlInstrumentationPoint("Factory", null, "MySql.Data", "T", "M", new[] { "System.Int32" }),
            new XmlInstrumentationPoint("Factory", null, "Microsoft.Data.SqlClient", "T", "Wide", null));

        Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.EqualTo(new[] { "ADONET", "MYSQLDATA", "SQLCLIENT" }));
    }

    [Test]
    public void Emit_NoMappedAssembly_EmitsEmptyArrays()
    {
        var source = EmitOrFail(new XmlInstrumentationPoint("Factory", null, "Hangfire.Core", "T", "M", new[] { "System.Int32" }));

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.Empty);
            Assert.That(GeneratedTable.ReadStringArray(source, "ExcludedActivitySourceAdditions"), Is.Empty);
            Assert.That(source, Does.Contain("DisabledOtelTraceInstrumentationIds = new string[0];"));
        });
    }

    [Test]
    public void Emit_Arrays_FollowTheTableInTheDefinitionsClass()
    {
        var source = EmitOrFail(new XmlInstrumentationPoint("Factory", null, "Npgsql", "T", "M", new[] { "System.Int32" }));

        var table = source.IndexOf("Table = new byte[]", StringComparison.Ordinal);
        var disabled = source.IndexOf("DisabledOtelTraceInstrumentationIds", StringComparison.Ordinal);
        var excluded = source.IndexOf("ExcludedActivitySourceAdditions", StringComparison.Ordinal);
        Assert.That(table, Is.LessThan(disabled));
        Assert.That(disabled, Is.LessThan(excluded));
        Assert.That(GeneratedTable.ReadDefinitions(source), Has.Count.EqualTo(1));
    }

    // Log-forwarding flag: a point whose tracer factory is one of NR's log-recording wrappers is marked, so the plugin
    // brackets its call with the OTel/NR log dedupe counter. Other points (and NR's logger-creation/registration
    // factories) are not. Verified through the production reader's DistroInstrumentationSite.LogForwarding.
    [Test]
    public void Emit_LogForwardingSites_AreFlagged_OthersAreNot()
    {
        var mel = new XmlInstrumentationPoint("MicrosoftLogging", null, "Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging.Logger", "Log", new[] { "System.Int32" });
        var melRegister = new XmlInstrumentationPoint("AddProviderRegistrationWrapper", null, "Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging.LoggerFactory", "AddProviderRegistration", new[] { "System.Int32" });
        var nlog = new XmlInstrumentationPoint("nlog", null, "NLog", "NLog.LoggerImpl", "Write", new[] { "System.String" });
        var sql = new XmlInstrumentationPoint("Factory", null, "Npgsql", "T", "M", new[] { "System.Int32" });

        var (definitions, sites) = GeneratedTable.Read(EmitOrFail(mel, melRegister, nlog, sql));

        Assert.Multiple(() =>
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                var expected = sites[i].TracerFactoryName == "MicrosoftLogging" || sites[i].TracerFactoryName == "nlog";
                Assert.That(sites[i].LogForwarding, Is.EqualTo(expected), sites[i].TracerFactoryName + "/" + sites[i].MethodName);
            }
        });
    }

    [Test]
    public void Emit_OverloadWideLogForwardingPoint_FlagsEveryArityVariant()
    {
        // NLog's Write match has no parameters attribute, so it expands to one record per arity; each must carry the flag.
        var (_, sites) = GeneratedTable.Read(EmitOrFail(new XmlInstrumentationPoint("nlog", null, "NLog", "NLog.LoggerImpl", "Write", null)));

        Assert.That(sites.Count, Is.EqualTo(IntegrationSourceEmitter.MaxParameters + 1));
        Assert.That(sites.All(s => s.LogForwarding), Is.True);
    }

    [Test]
    public void Emit_NLogLogForwardingPointDefaultOn_EmitsNrCompilesNLogLogForwardingTrueConst()
    {
        var source = EmitOrFail(
            new XmlInstrumentationPoint("nlog", null, "NLog", "NLog.LoggerImpl", "Write", null),
            new XmlInstrumentationPoint("MicrosoftLogging", null, "Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging.Logger", "Log", new[] { "System.Int32" }));

        Assert.That(source, Does.Contain("public const bool NrCompilesNLogLogForwarding = true;"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Emit_NoNLogOrNLogDefaultOff_EmitsNrCompilesNLogLogForwardingFalseConst(bool haveLog4Net)
    {
        // No NLog point (only MEL/log4net) -> false; and an NLog point that is default-off -> false (NR does not forward
        // NLog there, so OTel's bridge is left alone).
        var points = haveLog4Net
            ? new[] { new XmlInstrumentationPoint("log4net", null, "log4net", "log4net.Repository.Hierarchy.Logger", "CallAppenders", new[] { "System.Int32" }) }
            : new[] { new XmlInstrumentationPoint("nlog", null, "NLog", "NLog.LoggerImpl", "Write", new[] { "System.String" }, enabled: false) };

        Assert.That(EmitOrFail(points), Does.Contain("public const bool NrCompilesNLogLogForwarding = false;"));
    }

    [Test]
    public void Emit_SourcedPoints_EntriesCarrySourceFileDefaultOffAndPointKey()
    {
        var on = new XmlInstrumentationPoint("On", "Custom/On", "A", "B", "M", new[] { "System.Int32" }, "1.0", null);
        var off = new XmlInstrumentationPoint("Off", null, "A", "B", "Wide", null, enabled: false);
        var plugin = new XmlInstrumentationPoint("Plugin", null, "A", "B", "P", new List<string>());
        var files = new[]
        {
            new ShippedXmlFile("NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", 0x1234UL, compiled: true),
            new ShippedXmlFile("NewRelic.Providers.Wrapper.Kafka.Instrumentation.xml", 0x5678UL, compiled: false),
        };

        var emitted = IntegrationSourceEmitter.TryEmit(
            new[] { new SourcedXmlPoint(on, 0), new SourcedXmlPoint(off, 0), new SourcedXmlPoint(plugin, SourcedXmlPoint.PluginSourceFile) },
            files,
            Namespace,
            out var source,
            out var failureReason);
        Assert.That(emitted, Is.True, failureReason);
        var (_, sites) = GeneratedTable.Read(source);
        var manifest = GeneratedTable.ReadManifest(source);

        var onKey = PointKeyHasher.Compute(on, "B", VersionRangeConverter.Convert("1.0", null).RangeKey);
        var offKey = PointKeyHasher.Compute(off, "B", VersionRangeConverter.Convert(null, null).RangeKey);
        var pluginKey = PointKeyHasher.Compute(plugin, "B", VersionRangeConverter.Convert(null, null).RangeKey);

        Assert.Multiple(() =>
        {
            // Explicit points first (M, P), then the nine overload-wide records of Wide.
            Assert.That(sites.Select(s => s.MethodName + "/" + s.SourceFile + "/" + s.DefaultOff), Is.EqualTo(
                new[] { "M/0/False", "P/-1/False" }.Concat(Enumerable.Repeat("Wide/0/True", IntegrationSourceEmitter.MaxParameters + 1))));
            Assert.That(sites.Select(s => s.PointKey), Is.EqualTo(
                new[] { onKey, pluginKey }.Concat(Enumerable.Repeat(offKey, IntegrationSourceEmitter.MaxParameters + 1))));
            Assert.That(manifest.FileCount, Is.EqualTo(2));
            Assert.That(manifest.HomeName(1), Is.EqualTo("NewRelic.Providers.Wrapper.Kafka.Instrumentation.xml"));
            Assert.That(manifest.ContentHash(1), Is.EqualTo(0x5678UL));
            Assert.That(manifest.IsCompiled(1), Is.False);
            Assert.That(source, Does.Contain(", 2 files, "));
        });
    }

    [Test]
    public void Emit_DefaultOffPointsOfMappedAssembly_DisableNothing()
    {
        var source = EmitOrFail(
            new XmlInstrumentationPoint("Factory", null, "Npgsql", "T", "M", new[] { "System.Int32" }, enabled: false),
            new XmlInstrumentationPoint("Factory", null, "MySql.Data", "T", "Wide", null, enabled: false),
            new XmlInstrumentationPoint("Factory", null, "Microsoft.Data.SqlClient", "T", "M", new[] { "System.Int32" }));

        var (onlyOnIds, onlyOnSources) = OtelInstrumentationMap.Resolve(new[] { "Microsoft.Data.SqlClient" });

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadDefinitions(source), Has.Count.EqualTo(2 + IntegrationSourceEmitter.MaxParameters + 1));
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.EqualTo(onlyOnIds));
            Assert.That(onlyOnIds, Has.None.EqualTo("NPGSQL").And.None.EqualTo("MYSQLDATA"));
            Assert.That(GeneratedTable.ReadStringArray(source, "ExcludedActivitySourceAdditions"), Is.EqualTo(onlyOnSources));
        });
    }

    [Test]
    public void Emit_PlainPoints_HavePluginSourceAndNoFiles()
    {
        var source = EmitOrFail(new XmlInstrumentationPoint("Factory", null, "A", "B", "M", new[] { "System.Int32" }));

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.Read(source).Sites.Single().SourceFile, Is.EqualTo(-1));
            Assert.That(GeneratedTable.ReadManifest(source).FileCount, Is.EqualTo(0));
        });
    }

    private static int CountBytes(string line) => CountOccurrences(line, "0x");

    private static string EmitOrFail(params XmlInstrumentationPoint[] points)
    {
        var emitted = IntegrationSourceEmitter.TryEmit(points, Namespace, out var source, out var failureReason);
        Assert.That(emitted, Is.True, failureReason);
        return source;
    }
}
