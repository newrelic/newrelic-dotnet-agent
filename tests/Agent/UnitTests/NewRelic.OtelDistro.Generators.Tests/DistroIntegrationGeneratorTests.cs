// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NewRelic.Agent.Core.OtelDistro;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class DistroIntegrationGeneratorTests
{
    [Test]
    public void Generator_ValidXml_AddsSource()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" parameters=""System.Int32"" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var (diagnostics, generatedSources) = RunGenerator(xml);

        Assert.That(diagnostics, Is.Empty);
        Assert.That(generatedSources, Has.Count.EqualTo(1));
        Assert.That(generatedSources[0].HintName, Is.EqualTo(DistroIntegrationGenerator.GeneratedFileName));
        Assert.That(generatedSources[0].SourceText.ToString(), Does.Contain("NrDistroIntegration_"));
    }

    [Test]
    public void Generator_MatchWithoutParameters_ReportsNRDG001InfoAndEmits()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var (diagnostics, generatedSources) = RunGenerator(xml);

        var diagnostic = diagnostics.Single(d => d.Id == "NRDG001");
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Info));
            Assert.That(CountOccurrences(generatedSources[0].SourceText.ToString(), "public abstract class NrDistroIntegration_"), Is.EqualTo(9));
        });
    }

    [Test]
    public void Generator_DuplicateParameterlessMatch_ReportsNRDG003()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" />
              </match>
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var (diagnostics, generatedSources) = RunGenerator(xml);

        Assert.That(diagnostics.Select(d => d.Id), Has.Member("NRDG003"));
        var text = generatedSources[0].SourceText.ToString();
        Assert.That(CountOccurrences(text, "public abstract class NrDistroIntegration_"), Is.EqualTo(9));
    }

    [Test]
    public void Generator_Duplicate_ReportsNRDG003AndOneClass()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" parameters=""System.Int32"" />
              </match>
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" parameters=""System.Int32"" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var (diagnostics, generatedSources) = RunGenerator(xml);

        Assert.That(diagnostics.Select(d => d.Id), Has.Member("NRDG003"));
        var text = generatedSources[0].SourceText.ToString();
        Assert.That(CountOccurrences(text, "public abstract class NrDistroIntegration_"), Is.EqualTo(1));
    }

    [Test]
    public void Generator_MalformedXml_ReportsNRDG004()
    {
        const string xml = "<extension><instrumentation><unclosed></instrumentation></extension>";

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Select(d => d.Id), Has.Member("NRDG004"));
    }

    [Test]
    public void Generator_NineParameters_ReportsNRDG002()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" parameters=""System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32"" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Select(d => d.Id), Has.Member("NRDG002"));
    }

    [Test]
    public void Generator_StringOverTableLimit_ReportsNRDG005ErrorAndAddsNoSource()
    {
        var tooLong = new string('M', 65536);
        var xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""" + tooLong + @""" parameters=""System.Int32"" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var (diagnostics, generatedSources) = RunGenerator(xml);

        var diagnostic = diagnostics.Single(d => d.Id == "NRDG005");
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.GetMessage(), Does.Contain("65536 UTF-8 bytes"));
            Assert.That(generatedSources, Is.Empty);
        });
    }

    /// <summary>
    /// XML -> generator -> emitted Table bytes -> Core's reader. The inputs are the plugin's default build: Step0.xml
    /// (one explicit match) and the shipped Hangfire XML (its non-schema 'parameterTypes' attribute makes it an
    /// overload-wide match, so nine arity variants). The expected definitions are what the emitter produced before the
    /// table, as DistroCallTargetDefinition literals, in record order (stable-sorted by assembly name).
    /// </summary>
    [Test]
    public void RoundTrip_GeneratorOutput_ReadsBackEqual()
    {
        const string step0Xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<extension xmlns=""urn:newrelic-extension"">
  <instrumentation>
    <tracerFactory name=""NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper"">
      <match assemblyName=""Step0App"" className=""Step0App.Worker"">
        <exactMethodMatcher methodName=""DoWork"" parameters=""System.Int32"" />
      </match>
    </tracerFactory>
  </instrumentation>
</extension>";
        const string hangfireXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<extension xmlns=""urn:newrelic-extension"">
  <instrumentation>
    <tracerFactory name=""HangfirePerformerWrapper"">
      <match assemblyName=""Hangfire.Core"" className=""Hangfire.Server.CoreBackgroundJobPerformer"">
        <exactMethodMatcher methodName=""Perform"" parameterTypes=""Hangfire.Server.PerformContext"" />
      </match>
    </tracerFactory>
  </instrumentation>
</extension>";

        var (_, generatedSources) = RunGenerator(step0Xml, hangfireXml);

        var source = generatedSources.Single().SourceText.ToString();
        var (definitions, sites) = GeneratedTable.Read(source);

        // Hangfire.Core sorts before Step0App (ordinal), so its nine arity variants come first, in arity order.
        var expected = Enumerable.Range(0, 9)
            .Select(arity => new DistroCallTargetDefinition(
                "Hangfire.Core", "Hangfire.Server.CoreBackgroundJobPerformer", "Perform",
                Enumerable.Repeat("_", arity + 1).ToArray(),
                IntegrationTypeName("Hangfire.Core", "Hangfire.Server.CoreBackgroundJobPerformer", "Perform", "*" + arity)))
            .Append(new DistroCallTargetDefinition(
                "Step0App", "Step0App.Worker", "DoWork", new[] { "_", "System.Int32" },
                IntegrationTypeName("Step0App", "Step0App.Worker", "DoWork", "System.Int32")))
            .ToList();

        Assert.That(definitions, Has.Count.EqualTo(10));
        Assert.That(definitions.Select(GeneratedTable.Describe), Is.EqualTo(expected.Select(GeneratedTable.Describe)));

        // Site i belongs to record i: the XML point's tracer factory and metric (neither XML names a metric), tracer
        // arguments 0, the argument signature ("" for overload-wide) and the function id the emitter names the class by.
        var expectedSites = Enumerable.Range(0, 9)
            .Select(arity => new DistroInstrumentationSite(
                "HangfirePerformerWrapper", null, 0u,
                "Hangfire.Core", "Hangfire.Server.CoreBackgroundJobPerformer", "Perform", "",
                FunctionIdHasher.Compute("Hangfire.Core", "Hangfire.Server.CoreBackgroundJobPerformer", "Perform", "*" + arity)))
            .Append(new DistroInstrumentationSite(
                "NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper", null, 0u,
                "Step0App", "Step0App.Worker", "DoWork", "System.Int32",
                FunctionIdHasher.Compute("Step0App", "Step0App.Worker", "DoWork", "System.Int32")))
            .ToList();

        Assert.That(sites.Select(GeneratedTable.Describe), Is.EqualTo(expectedSites.Select(GeneratedTable.Describe)));

        // Step0.xml is given first but sorts last, so each class's SiteIndex must follow its record, not emit order.
        var siteIndexByType = GeneratedTable.SiteIndexByIntegrationType(source, DistroIntegrationGenerator.GeneratedNamespace);
        Assert.That(
            definitions.Select(d => d.IntegrationTypeName).ToDictionary(t => t, t => siteIndexByType[t]),
            Is.EqualTo(definitions.Select((d, i) => (d.IntegrationTypeName, i)).ToDictionary(p => p.IntegrationTypeName, p => p.i)));
        Assert.That(siteIndexByType, Has.Count.EqualTo(10));
    }

    private static string IntegrationTypeName(string assembly, string type, string method, string argumentSignature) =>
        DistroIntegrationGenerator.GeneratedNamespace + ".NrDistroIntegration_"
        + FunctionIdHasher.Compute(assembly, type, method, argumentSignature).ToString("X16", CultureInfo.InvariantCulture);

    [Test]
    public void Generator_SameKeyDisjointRanges_KeepsBothSiblingsInXmlOrderWithDistinctIds()
    {
        var xml = Extension(
            Factory("Old", Match(@"minVersion=""1.0.0.0"" maxVersion=""6.0.0.0""", Method("M", "System.Int32")))
            + Factory("New", Match(@"minVersion=""6.0.0.0""", Method("M", "System.Int32"))));

        var (diagnostics, generatedSources) = RunGenerator(xml);
        var source = generatedSources.Single().SourceText.ToString();
        var (definitions, sites) = GeneratedTable.Read(source);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(definitions.Select(GeneratedTable.Describe), Is.EqualTo(new[]
            {
                $"A | B | M | [_,System.Int32] | {IntegrationTypeName("A", "B", "M", "System.Int32", "1.0.0-5.65535.65535")} | 1.0.0-5.65535.65535",
                $"A | B | M | [_,System.Int32] | {IntegrationTypeName("A", "B", "M", "System.Int32", "6.0.0-65535.65535.65535")} | 6.0.0-65535.65535.65535"
            }));
            Assert.That(sites.Select(s => s.TracerFactoryName), Is.EqualTo(new[] { "Old", "New" }));
            Assert.That(sites.Select(s => s.FunctionId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(CountOccurrences(source, "public abstract class NrDistroIntegration_"), Is.EqualTo(2));
        });
    }

    [Test]
    public void Generator_SameKeySameRange_ReportsNRDG003AndKeepsTheFirst()
    {
        var xml = Extension(
            Factory("First", Match(@"minVersion=""2.0""", Method("M", "System.Int32")))
            + Factory("Second", Match(@"minVersion=""2.0.0.0""", Method("M", "System.Int32"))));

        var (diagnostics, generatedSources) = RunGenerator(xml);
        var (definitions, sites) = GeneratedTable.Read(generatedSources.Single().SourceText.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "NRDG003" }));
            Assert.That(definitions, Has.Count.EqualTo(1));
            Assert.That(sites.Single().TracerFactoryName, Is.EqualTo("First"));
        });
    }

    [Test]
    public void Generator_UnrangedMatch_KeepsThePreChangeFunctionId()
    {
        var xml = Extension(@"<tracerFactory name=""F""><match assemblyName=""Step0App"" className=""Step0App.Worker"">"
            + Method("DoWork", "System.Int32") + "</match></tracerFactory>");

        var (_, generatedSources) = RunGenerator(xml);
        var (_, sites) = GeneratedTable.Read(generatedSources.Single().SourceText.ToString());

        Assert.That(sites.Single().FunctionId, Is.EqualTo(0xD43654B7FA81DBC4UL));
    }

    [Test]
    public void Generator_OverloadWideBeforeExplicitInXml_EmitsExplicitFirst()
    {
        // OTel native installs the first matching definition per method, so the explicit entry must precede the
        // overload-wide one of the same arity, whatever the XML order (agent mode prefers the exact key).
        var xml = Extension(
            Factory("Wide", Match("", Method("M")))
            + Factory("Exact", Match("", Method("M", "System.Int32"))));

        var (_, generatedSources) = RunGenerator(xml);
        var (definitions, sites) = GeneratedTable.Read(generatedSources.Single().SourceText.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(definitions, Has.Count.EqualTo(10));
            Assert.That(sites[0].TracerFactoryName, Is.EqualTo("Exact"));
            Assert.That(sites.Skip(1).Select(s => s.TracerFactoryName), Is.All.EqualTo("Wide"));
            Assert.That(definitions.Skip(1).Select(d => d.TargetSignatureTypes.Length), Is.EqualTo(Enumerable.Range(1, 9)));
        });
    }

    [Test]
    public void Generator_RangedCleanXml_ReportsNoRangeDiagnostics()
    {
        var xml = Extension(Factory("F",
            Match(@"maxVersion=""8.0.33""", Method("M", "System.Int32"))
            + Match(@"minVersion=""8.0.33"" maxVersion=""9.7.0""", Method("M", "System.Int32"))
            + Match(@"minVersion=""4"" maxVersion=""5.0""", Method("N"))));

        var (diagnostics, generatedSources) = RunGenerator(xml);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Where(d => d.Id != "NRDG001"), Is.Empty);
            Assert.That(GeneratedTable.ReadDefinitions(generatedSources.Single().SourceText.ToString()), Has.Count.EqualTo(11));
        });
    }

    [Test]
    public void Generator_InvalidBound_ReportsNRDG006AndEmitsUnbounded()
    {
        var xml = Extension(Factory("F", Match(@"minVersion=""1.0.0-beta"" maxVersion=""abc""", Method("M", "System.Int32"))));

        var (diagnostics, generatedSources) = RunGenerator(xml);
        var definition = GeneratedTable.ReadDefinitions(generatedSources.Single().SourceText.ToString()).Single();

        // "1.0.0-beta" splits into three parts and stoi reads "0-beta" as 0, so it is valid (1.0.0); "abc" is not.
        var diagnostic = diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("NRDG006"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(diagnostic.GetMessage(), Is.EqualTo("The maxVersion 'abc' of the match for A/B.M(System.Int32) is not a version; it is treated as no bound, as agent mode does"));
            Assert.That(GeneratedTable.Describe(definition), Does.EndWith(" | 1.0.0-65535.65535.65535"));
        });
    }

    [Test]
    public void Generator_InvalidMinVersion_ReportsNRDG006NamingMinVersion()
    {
        var xml = Extension(Factory("F", Match(@"minVersion=""x""", Method("M"))));

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Single(d => d.Id == "NRDG006").GetMessage(), Is.EqualTo("The minVersion 'x' of the match for A/B.M(any parameters) is not a version; it is treated as no bound, as agent mode does"));
    }

    [Test]
    public void Generator_RevisionBounds_ReportNRDG007PerBoundWithTheEmittedBound()
    {
        var xml = Extension(Factory("F", Match(@"minVersion=""1.2.3.4"" maxVersion=""2.0.0.1""", Method("M", "System.Int32"))));

        var (diagnostics, generatedSources) = RunGenerator(xml);
        var definition = GeneratedTable.ReadDefinitions(generatedSources.Single().SourceText.ToString()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "NRDG007", "NRDG007" }));
            Assert.That(diagnostics.Select(d => d.Severity), Is.All.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(diagnostics[0].GetMessage(), Is.EqualTo("The minVersion '1.2.3.4' of the match for A/B.M(System.Int32) has a non-zero revision, which OTel cannot express; it was widened to 1.2.3 (inclusive)"));
            Assert.That(diagnostics[1].GetMessage(), Is.EqualTo("The maxVersion '2.0.0.1' of the match for A/B.M(System.Int32) has a non-zero revision, which OTel cannot express; it was widened to 2.0.0 (inclusive)"));
            Assert.That(GeneratedTable.Describe(definition), Does.EndWith(" | 1.2.3-2.0.0"));
        });
    }

    [Test]
    public void Generator_RevisionWidenedToTheDefaultRange_StillReportsNRDG007()
    {
        var xml = Extension(Factory("F", Match(@"minVersion=""0.0.0.5""", Method("M", "System.Int32"))));

        var (diagnostics, generatedSources) = RunGenerator(xml);
        var (_, sites) = GeneratedTable.Read(generatedSources.Single().SourceText.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Single().Id, Is.EqualTo("NRDG007"));
            Assert.That(sites.Single().FunctionId, Is.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "System.Int32")));
        });
    }

    [Test]
    public void Generator_EmptyRange_ReportsNRDG008AndDropsTheEntry()
    {
        var xml = Extension(Factory("F",
            Match(@"minVersion=""2.0"" maxVersion=""2.0""", Method("M", "System.Int32"))
            + Match("", Method("Kept", "System.Int32"))));

        var (diagnostics, generatedSources) = RunGenerator(xml);
        var definitions = GeneratedTable.ReadDefinitions(generatedSources.Single().SourceText.ToString());

        var diagnostic = diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("NRDG008"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(diagnostic.GetMessage(), Is.EqualTo("The match for A/B.M(System.Int32) has an empty version range (minVersion '2.0', maxVersion '2.0'); no version can match, so it was skipped"));
            Assert.That(definitions.Select(d => d.TargetMethod), Is.EqualTo(new[] { "Kept" }));
        });
    }

    [Test]
    public void Generator_MySqlShapedExplicitOutOfRange_ReportsNRDG009()
    {
        var xml = Extension(Factory("F",
            Match(@"maxVersion=""8.0.33""", Method("ExecuteNonQuery"))
            + Match(@"minVersion=""9.7.0""", Method("ExecuteNonQuery", "System.Threading.CancellationToken"))));

        var (diagnostics, _) = RunGenerator(xml);

        var diagnostic = diagnostics.Single(d => d.Id != "NRDG001");
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("NRDG009"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(diagnostic.GetMessage(), Is.EqualTo(
                "The overload-wide match for A/B.ExecuteNonQuery (versions 0.0.0-8.0.32) is in range for versions where no explicit match of the same method with 1 parameters is "
                + "(explicit versions: 9.7.0-65535.65535.65535); agent mode instruments nothing there, the distro instruments the overload"));
        });
    }

    [Test]
    public void Generator_MongoDbShapedExplicitUnbounded_ReportsNoNRDG009()
    {
        var xml = Extension(Factory("F",
            Match("", Method("CreateCollection", "System.String"))
            + Match(@"minVersion=""3.0.0""", Method("CreateCollection"))));

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Select(d => d.Id), Has.No.Member("NRDG009"));
    }

    [Test]
    public void Generator_ExplicitRangesCoverTheOverloadWideRangeTogether_ReportsNoNRDG009()
    {
        var xml = Extension(Factory("F",
            Match(@"minVersion=""2.0""", Method("M", "System.Int32"))
            + Match(@"maxVersion=""2.0""", Method("M", "System.String"))
            + Match(@"maxVersion=""3.0""", Method("M", "System.Int64"))
            + Match(@"maxVersion=""1.0""", Method("M", "System.Int16"))
            + Match(@"minVersion=""1.0""", Method("M"))));

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Select(d => d.Id), Has.No.Member("NRDG009"));
    }

    [Test]
    public void Generator_ExplicitRangeEndsBeforeOverloadWideRange_ReportsNRDG009()
    {
        var xml = Extension(Factory("F",
            Match(@"maxVersion=""1.0""", Method("M", "System.Int32"))
            + Match("", Method("M"))));

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Single(d => d.Id == "NRDG009").GetMessage(), Does.Contain("(explicit versions: 0.0.0-0.65535.65535)"));
    }

    [Test]
    public void Generator_GapBetweenExplicitRanges_ReportsNRDG009()
    {
        var xml = Extension(Factory("F",
            Match(@"maxVersion=""1.0""", Method("M", "System.Int32"))
            + Match(@"minVersion=""2.0""", Method("M", "System.String"))
            + Match("", Method("M"))));

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Single(d => d.Id == "NRDG009").GetMessage(), Does.Contain("(versions 0.0.0-65535.65535.65535) is in range for versions where no explicit match of the same method with 1 parameters is").And.Contain("(explicit versions: 0.0.0-0.65535.65535, 2.0.0-65535.65535.65535)"));
    }

    [Test]
    public void Generator_OverloadWideWithExplicitOfOtherMethodOrClass_ReportsNoNRDG009()
    {
        var xml = Extension(Factory("F",
            Match(@"minVersion=""2.0""", Method("Other", "System.Int32"))
            + @"<match assemblyName=""A"" className=""C"" minVersion=""2.0""><exactMethodMatcher methodName=""M"" parameters=""System.Int32"" /></match>"
            + Match("", Method("M"))));

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Select(d => d.Id), Has.No.Member("NRDG009"));
    }

    [Test]
    public void Generator_SameKeyOverlappingRangesDifferentFactories_ReportsNRDG010()
    {
        var xml = Extension(
            Factory("One", Match(@"minVersion=""1.0"" maxVersion=""3.0""", Method("M", "System.Int32")))
            + Factory("Two", Match(@"minVersion=""2.0""", Method("M", "System.Int32"))));

        var (diagnostics, generatedSources) = RunGenerator(xml);

        var diagnostic = diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("NRDG010"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(diagnostic.GetMessage(), Is.EqualTo(
                "The matches for A/B.M(System.Int32) have overlapping version ranges 1.0.0-2.65535.65535 ('One') and 2.0.0-65535.65535.65535 ('Two') with different tracer factories; "
                + "the distro uses the first in XML order, agent mode's choice is unspecified"));
            Assert.That(GeneratedTable.ReadDefinitions(generatedSources.Single().SourceText.ToString()), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Generator_SameKeyOverlappingRangesSameFactory_ReportsNoNRDG010()
    {
        var xml = Extension(Factory("One",
            Match(@"minVersion=""1.0"" maxVersion=""3.0""", Method("M", "System.Int32"))
            + Match(@"minVersion=""2.0""", Method("M", "System.Int32"))));

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Generator_ClassNameSegmentsEmptyOrPadded_AreSkipped()
    {
        var xml = Extension(@"<tracerFactory name=""F""><match assemblyName=""A"" className=""B, C,D ,"">"
            + Method("M", "System.Int32") + "</match></tracerFactory>");

        var (diagnostics, generatedSources) = RunGenerator(xml);
        var definitions = GeneratedTable.ReadDefinitions(generatedSources.Single().SourceText.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(definitions.Select(d => d.TargetType), Is.EqualTo(new[] { "B" }));
        });
    }

    [Test]
    public void Generator_PluginShapedXmlSet_EmitsTheDisableAndExcludeArraysOfItsLibraries()
    {
        // A representative shipped-XML subset (not the plugin's actual default build, which now compiles every shipped
        // file -- see ShippedInstrumentationXmlTests): the shipped Hangfire, Couchbase3, Sql and HttpClient XML. Hangfire
        // and Couchbase have no OTel counterpart; Sql blocks ADO.NET (and duplicates the provider IDs); the HttpClient
        // XML alone does not disable OTel's HTTPCLIENT, which on .NET Framework is HttpWebRequest-based (that needs the
        // HttpWebRequest XML too).
        var root = typeof(DistroIntegrationGeneratorTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "RepositoryRootDirectory").Value;
        var wrappers = new[] { "Hangfire", "Couchbase3", "Sql", "HttpClient" };
        var xml = wrappers
            .Select(name => System.IO.File.ReadAllText(System.IO.Path.Combine(root, "src", "Agent", "NewRelic", "Agent", "Extensions", "Providers", "Wrapper", name, "Instrumentation.xml")))
            .ToArray();

        var (_, generatedSources) = RunGenerator(xml);
        var source = generatedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"),
                Is.EqualTo(new[] { "ADONET", "MYSQLCONNECTOR", "MYSQLDATA", "NPGSQL", "ORACLEMDA", "SQLCLIENT" }));
            Assert.That(GeneratedTable.ReadStringArray(source, "ExcludedActivitySourceAdditions"), Is.EqualTo(new[]
            {
                "OpenTelemetry.AutoInstrumentation.AdoNet",
                "OpenTelemetry.Instrumentation.SqlClient"
            }));
        });
    }

    [Test]
    public void Generator_MapAssemblyOnlyInADisabledFactory_DisablesNothing()
    {
        // A factory with enabled="false" reaches the table only as default-off points, which do not count as present.
        var xml = Extension(@"<tracerFactory name=""Off"" enabled=""false""><match assemblyName=""RabbitMQ.Client"" className=""B"">"
            + Method("M", "System.Int32") + "</match></tracerFactory>"
            + Factory("On", @"<match assemblyName=""Hangfire.Core"" className=""B"">" + Method("M", "System.Int32") + "</match>"));

        var (_, generatedSources) = RunGenerator(xml);
        var source = generatedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.Empty);
            Assert.That(GeneratedTable.ReadStringArray(source, "ExcludedActivitySourceAdditions"), Is.Empty);
        });
    }

    [Test]
    public void Generator_MapAssemblyInAnEnabledFactory_DisablesItsIds()
    {
        var xml = Extension(Factory("On", @"<match assemblyName=""RabbitMQ.Client"" className=""B"">" + Method("M", "System.Int32") + "</match>"));

        var (_, generatedSources) = RunGenerator(xml);
        var source = generatedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.EqualTo(new[] { "RABBITMQ" }));
            Assert.That(GeneratedTable.ReadStringArray(source, "ExcludedActivitySourceAdditions"), Is.EqualTo(new[] { "OpenTelemetry.AutoInstrumentation.RabbitMq" }));
        });
    }

    private const string SqlWrapperPath = @"C:\repo\src\Agent\NewRelic\Agent\Extensions\Providers\Wrapper\Sql\Instrumentation.xml";
    private const string SqlHomeName = "NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
    private const string KafkaWrapperPath = @"C:\repo\src\Agent\NewRelic\Agent\Extensions\Providers\Wrapper\Kafka\Instrumentation.xml";
    private const string KafkaHomeName = "NewRelic.Providers.Wrapper.Kafka.Instrumentation.xml";
    private const string PluginXmlPath = @"C:\repo\src\Agent\NewRelic\OtelDistro\NewRelic.OtelDistro.Plugin\Instrumentation\Step0.xml";

    [Test]
    public void Emit_DisabledFactory_RecordsFlaggedDefaultOff()
    {
        var xml = Extension(
            Factory("On", Match("", Method("M", "System.Int32")))
            + @"<tracerFactory name=""Off"" enabled=""false"">" + Match("", Method("N", "System.Int32")) + "</tracerFactory>");

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, xml));
        var source = generatedSources.Single().SourceText.ToString();
        var (definitions, sites) = GeneratedTable.Read(source);
        var manifest = GeneratedTable.ReadManifest(source);
        var range = VersionRangeConverter.Convert(null, null);
        var offPoint = new XmlInstrumentationPoint("Off", null, "A", "B", "N", new[] { "System.Int32" }, enabled: false);
        var onPoint = new XmlInstrumentationPoint("On", null, "A", "B", "M", new[] { "System.Int32" });

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(definitions.Select(d => d.TargetMethod), Is.EqualTo(new[] { "M", "N" }));
            Assert.That(sites.Select(s => s.DefaultOff), Is.EqualTo(new[] { false, true }));
            Assert.That(sites.Select(s => s.TracerFactoryName), Is.EqualTo(new[] { "On", "Off" }));
            Assert.That(sites.Select(s => s.SourceFile), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(sites[0].PointKey, Is.EqualTo(PointKeyHasher.Compute(onPoint, "B", range.RangeKey)));
            Assert.That(sites[1].PointKey, Is.EqualTo(PointKeyHasher.Compute(offPoint, "B", range.RangeKey)));
            Assert.That(manifest.FileCount, Is.EqualTo(1));
            Assert.That(manifest.HomeName(0), Is.EqualTo(SqlHomeName));
            Assert.That(manifest.ContentHash(0), Is.EqualTo(XmlContentHasher.Compute(xml)));
            Assert.That(manifest.IsCompiled(0), Is.True);
        });
    }

    [Test]
    public void Emit_HashOnlyFile_InManifestNotCompiledNoRecords()
    {
        var compiled = Extension(Factory("F", Match("", Method("M", "System.Int32"))));
        // Hash-only content is never parsed: a point in it adds no record, and malformed XML reports nothing.
        var hashOnly = Extension(Factory("G", @"<match assemblyName=""Kafka"" className=""K"">" + Method("Produce", "System.Int32") + "</match>"));
        const string malformed = "<extension><unclosed></extension>";
        var miscPath = @"C:\repo\src\Agent\Miscellaneous\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml";

        var (diagnostics, generatedSources) = RunGenerator(
            new XmlInput(PluginXmlPath, compiled),
            new XmlInput(KafkaWrapperPath, hashOnly, HashOnly: true),
            new XmlInput(miscPath, malformed, HashOnly: true));
        var source = generatedSources.Single().SourceText.ToString();
        var (definitions, sites) = GeneratedTable.Read(source);
        var manifest = GeneratedTable.ReadManifest(source);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(definitions.Select(d => d.TargetAssembly), Is.EqualTo(new[] { "A" }));
            Assert.That(sites.Single().SourceFile, Is.EqualTo(-1));
            Assert.That(manifest.FileCount, Is.EqualTo(2));
            // Sorted by home name (ordinal ignoring case), so the order does not depend on the item order.
            Assert.That(manifest.HomeName(0), Is.EqualTo(KafkaHomeName));
            Assert.That(manifest.ContentHash(0), Is.EqualTo(XmlContentHasher.Compute(hashOnly)));
            Assert.That(manifest.IsCompiled(0), Is.False);
            Assert.That(manifest.HomeName(1), Is.EqualTo("NewRelic.Providers.Wrapper.Misc.Instrumentation.xml"));
            Assert.That(manifest.ContentHash(1), Is.EqualTo(XmlContentHasher.Compute(malformed)));
            Assert.That(manifest.IsCompiled(1), Is.False);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Emit_CompiledFileAlsoListedHashOnly_IsCompiled(bool hashOnlyFirst)
    {
        var xml = Extension(Factory("F", Match("", Method("M", "System.Int32"))));
        var homeCopy = @"C:\repo\src\Agent\newrelichome_x64_coreclr\extensions\" + SqlHomeName;
        var compiledInput = new XmlInput(homeCopy, xml);
        var hashOnlyInput = new XmlInput(SqlWrapperPath, xml + " ", HashOnly: true);

        var (diagnostics, generatedSources) = RunGenerator(hashOnlyFirst ? new[] { hashOnlyInput, compiledInput } : new[] { compiledInput, hashOnlyInput });
        var source = generatedSources.Single().SourceText.ToString();
        var (_, sites) = GeneratedTable.Read(source);
        var manifest = GeneratedTable.ReadManifest(source);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(manifest.FileCount, Is.EqualTo(1));
            Assert.That(manifest.HomeName(0), Is.EqualTo(SqlHomeName));
            Assert.That(manifest.IsCompiled(0), Is.True);
            Assert.That(manifest.ContentHash(0), Is.EqualTo(XmlContentHasher.Compute(xml)), "the compiled file's hash, not the hash-only copy's");
            Assert.That(sites.Single().SourceFile, Is.EqualTo(0));
        });
    }

    [Test]
    public void Emit_TwoCompiledFilesWithOneHomeName_ShareTheFirstFilesEntry()
    {
        var first = Extension(Factory("F", Match("", Method("M", "System.Int32"))));
        var second = Extension(Factory("F", Match("", Method("N", "System.Int32"))));
        var homeCopy = @"C:\repo\src\Agent\newrelichome_x64_coreclr\extensions\" + SqlHomeName;

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, first), new XmlInput(homeCopy, second));
        var source = generatedSources.Single().SourceText.ToString();
        var (_, sites) = GeneratedTable.Read(source);
        var manifest = GeneratedTable.ReadManifest(source);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(manifest.FileCount, Is.EqualTo(1));
            Assert.That(manifest.ContentHash(0), Is.EqualTo(XmlContentHasher.Compute(first)));
            Assert.That(sites.Select(s => s.MethodName + "/" + s.SourceFile), Is.EqualTo(new[] { "M/0", "N/0" }));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Emit_DefaultOnBeatsDefaultOffOnSameKey_EitherOrder(bool offFirst)
    {
        // Same tracer factory: the default-on copy wins whatever the order (R4 keeps both only across factories).
        var off = Extension(@"<tracerFactory name=""SameFactory"" enabled=""false"">" + Match("", Method("M", "System.Int32")) + "</tracerFactory>");
        var on = Extension(Factory("SameFactory", Match("", Method("M", "System.Int32"))));
        var offInput = new XmlInput(SqlWrapperPath, off);
        var onInput = new XmlInput(KafkaWrapperPath, on);

        var (diagnostics, generatedSources) = RunGenerator(offFirst ? new[] { offInput, onInput } : new[] { onInput, offInput });
        var source = generatedSources.Single().SourceText.ToString();
        var (_, sites) = GeneratedTable.Read(source);
        var manifest = GeneratedTable.ReadManifest(source);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "NRDG003" }));
            Assert.That(sites, Has.Count.EqualTo(1));
            Assert.That(sites[0].TracerFactoryName, Is.EqualTo("SameFactory"));
            Assert.That(sites[0].DefaultOff, Is.False);
            Assert.That(manifest.HomeName(sites[0].SourceFile), Is.EqualTo(KafkaHomeName));
        });
    }

    [Test]
    public void Emit_SameKeySameFactoryBothDefaultOff_FirstWins()
    {
        var first = @"<tracerFactory name=""Same"" metricName=""First"" enabled=""false"">" + Match("", Method("M", "System.Int32")) + "</tracerFactory>";
        var second = @"<tracerFactory name=""Same"" metricName=""Second"" enabled=""false"">" + Match("", Method("M", "System.Int32")) + "</tracerFactory>";

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, Extension(first + second)));
        var (_, sites) = GeneratedTable.Read(generatedSources.Single().SourceText.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "NRDG003" }));
            Assert.That(sites.Select(s => s.MetricName + "/" + s.DefaultOff), Is.EqualTo(new[] { "First/True" }));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Emit_SameKeyDifferentFactoriesOneDefaultOff_KeepsBothAndTheDefaultOnKeepsThePlainId(bool offFirst)
    {
        var off = @"<tracerFactory name=""OffFactory"" enabled=""false"">" + Match(@"minVersion=""2.0""", Method("M", "System.Int32")) + "</tracerFactory>";
        var on = Factory("OnFactory", Match(@"minVersion=""2.0""", Method("M", "System.Int32")));

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, Extension(offFirst ? off + on : on + off)));
        var source = generatedSources.Single().SourceText.ToString();
        var (definitions, sites) = GeneratedTable.Read(source);
        var range = VersionRangeConverter.Convert("2.0", null);
        var onSite = sites.Single(s => s.TracerFactoryName == "OnFactory");
        var offSite = sites.Single(s => s.TracerFactoryName == "OffFactory");

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(sites, Has.Count.EqualTo(2));
            Assert.That(onSite.DefaultOff, Is.False);
            Assert.That(offSite.DefaultOff, Is.True);
            // The default-on point keeps the id it had before default-off points were emitted; the other folds in its factory.
            Assert.That(onSite.FunctionId, Is.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "System.Int32", range.RangeKey)));
            Assert.That(offSite.FunctionId, Is.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "System.Int32", range.RangeKey + "|OffFactory")));
            Assert.That(definitions.Select(d => d.IntegrationTypeName).Distinct().Count(), Is.EqualTo(2));
            Assert.That(onSite.PointKey, Is.EqualTo(PointKeyHasher.Compute(new XmlInstrumentationPoint("OnFactory", null, "A", "B", "M", new[] { "System.Int32" }, "2.0", null), "B", range.RangeKey)));
            Assert.That(offSite.PointKey, Is.EqualTo(PointKeyHasher.Compute(new XmlInstrumentationPoint("OffFactory", null, "A", "B", "M", new[] { "System.Int32" }, "2.0", null, enabled: false), "B", range.RangeKey)));
            Assert.That(GeneratedTable.SiteIndexByIntegrationType(source, DistroIntegrationGenerator.GeneratedNamespace), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Emit_SameKeyDifferentFactoriesBothDefaultOffOverloadWide_KeepsBothWithDistinctClasses()
    {
        // The Sql XML's shape: DataReaderTracer and DataReaderTracerAsync both match Npgsql TryFastRead (overload-wide).
        var first = @"<tracerFactory name=""DataReaderTracer"" enabled=""false"">" + Match("", Method("TryFastRead")) + "</tracerFactory>";
        var second = @"<tracerFactory name=""DataReaderTracerAsync"" enabled=""false"">" + Match("", Method("TryFastRead")) + "</tracerFactory>";

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, Extension(first + second)));
        var source = generatedSources.Single().SourceText.ToString();
        var (definitions, sites) = GeneratedTable.Read(source);
        var range = VersionRangeConverter.Convert(null, null);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Select(d => d.Id), Is.All.EqualTo("NRDG001"));
            Assert.That(sites, Has.Count.EqualTo(2 * (IntegrationSourceEmitter.MaxParameters + 1)));
            Assert.That(sites.Select(s => s.FunctionId).Distinct().Count(), Is.EqualTo(sites.Count));
            Assert.That(definitions.Select(d => d.IntegrationTypeName).Distinct().Count(), Is.EqualTo(sites.Count));
            Assert.That(sites.Where(s => s.TracerFactoryName == "DataReaderTracer").Select(s => s.FunctionId), Is.EqualTo(
                Enumerable.Range(0, IntegrationSourceEmitter.MaxParameters + 1).Select(arity => FunctionIdHasher.Compute("A", "B", "TryFastRead", "*" + arity))));
            Assert.That(sites.Where(s => s.TracerFactoryName == "DataReaderTracerAsync").Select(s => s.FunctionId), Is.EqualTo(
                Enumerable.Range(0, IntegrationSourceEmitter.MaxParameters + 1).Select(arity => FunctionIdHasher.Compute("A", "B", "TryFastRead", "*" + arity, "|DataReaderTracerAsync"))));
            Assert.That(sites.Where(s => s.TracerFactoryName == "DataReaderTracerAsync").Select(s => s.PointKey), Is.All.EqualTo(
                PointKeyHasher.Compute(new XmlInstrumentationPoint("DataReaderTracerAsync", null, "A", "B", "TryFastRead", null, enabled: false), "B", range.RangeKey)));
            Assert.That(sites.Select(s => s.DefaultOff), Is.All.True);
        });
    }

    [Test]
    public void Emit_SameKeyDifferentFactoriesBothDefaultOn_FirstWins()
    {
        var xml = Extension(Factory("First", Match("", Method("M", "System.Int32"))) + Factory("Second", Match("", Method("M", "System.Int32"))));

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, xml));
        var (_, sites) = GeneratedTable.Read(generatedSources.Single().SourceText.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "NRDG003" }));
            Assert.That(sites.Select(s => s.TracerFactoryName), Is.EqualTo(new[] { "First" }));
            Assert.That(sites.Single().FunctionId, Is.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "System.Int32")));
        });
    }

    [Test]
    public void Emit_SameKeyTwoDefaultOnAndOneDefaultOffFactory_KeepsFirstOnAndTheOff()
    {
        var xml = Extension(
            Factory("On1", Match("", Method("M", "System.Int32")))
            + Factory("On2", Match("", Method("M", "System.Int32")))
            + @"<tracerFactory name=""Off"" enabled=""false"">" + Match("", Method("M", "System.Int32")) + "</tracerFactory>");

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, xml));
        var (_, sites) = GeneratedTable.Read(generatedSources.Single().SourceText.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "NRDG003" }));
            Assert.That(sites.Select(s => s.TracerFactoryName), Is.EqualTo(new[] { "On1", "Off" }));
            Assert.That(sites[0].FunctionId, Is.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "System.Int32")));
            Assert.That(sites[1].FunctionId, Is.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "System.Int32", "|Off")));
        });
    }

    [Test]
    public void Emit_DefaultOffOnlyResidue_NRDG009SaysDefaultOff()
    {
        var xml = Extension(@"<tracerFactory name=""Off"" enabled=""false"">"
            + Match(@"maxVersion=""8.0.33""", Method("Read"))
            + Match(@"minVersion=""9.7.0""", Method("Read", "System.Threading.CancellationToken"))
            + "</tracerFactory>");

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Single(d => d.Id == "NRDG009").GetMessage(), Does.StartWith("The default-off overload-wide match for A/B.Read (versions 0.0.0-8.0.32) is in range"));
    }

    [Test]
    public void Emit_ResidueWithADefaultOnExplicitMatch_NRDG009DoesNotSayDefaultOff()
    {
        var xml = Extension(
            @"<tracerFactory name=""Off"" enabled=""false"">" + Match(@"maxVersion=""8.0.33""", Method("Read")) + "</tracerFactory>"
            + Factory("On", Match(@"minVersion=""9.7.0""", Method("Read", "System.Threading.CancellationToken"))));

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Single(d => d.Id == "NRDG009").GetMessage(), Does.StartWith("The overload-wide match for A/B.Read "));
    }

    [Test]
    public void Emit_DefaultOffOnlyOverlap_NRDG010SaysDefaultOff()
    {
        var xml = Extension(
            @"<tracerFactory name=""One"" enabled=""false"">" + Match(@"minVersion=""1.0"" maxVersion=""3.0""", Method("M", "System.Int32")) + "</tracerFactory>"
            + @"<tracerFactory name=""Two"" enabled=""false"">" + Match(@"minVersion=""2.0""", Method("M", "System.Int32")) + "</tracerFactory>");

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Single().GetMessage(), Is.EqualTo(
            "The default-off matches for A/B.M(System.Int32) have overlapping version ranges 1.0.0-2.65535.65535 ('One') and 2.0.0-65535.65535.65535 ('Two') with different tracer factories; "
            + "the distro uses the first in XML order, agent mode's choice is unspecified"));
    }

    [Test]
    public void Emit_OverlapWithOneDefaultOn_NRDG010DoesNotSayDefaultOff()
    {
        var xml = Extension(
            @"<tracerFactory name=""One"" enabled=""false"">" + Match(@"minVersion=""1.0"" maxVersion=""3.0""", Method("M", "System.Int32")) + "</tracerFactory>"
            + Factory("Two", Match(@"minVersion=""2.0""", Method("M", "System.Int32"))));

        var (diagnostics, _) = RunGenerator(xml);

        Assert.That(diagnostics.Single().GetMessage(), Does.StartWith("The matches for A/B.M(System.Int32) have overlapping"));
    }

    [Test]
    public void Emit_DefaultOffOnlyAssembly_DoesNotDisableOtelAndReportsNrdg011()
    {
        var xml = Extension(@"<tracerFactory name=""Off"" enabled=""false""><match assemblyName=""RabbitMQ.Client"" className=""B"">"
            + Method("M", "System.Int32") + Method("N", "System.Int32") + "</match></tracerFactory>"
            + @"<tracerFactory name=""Off2"" enabled=""false""><match assemblyName=""RabbitMQ.Client"" className=""B"">" + Method("P", "System.Int32") + "</match></tracerFactory>"
            + Factory("On", @"<match assemblyName=""Hangfire.Core"" className=""B"">" + Method("M", "System.Int32") + "</match>"));

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, xml));
        var source = generatedSources.Single().SourceText.ToString();
        var (_, sites) = GeneratedTable.Read(source);
        var nrdg011 = diagnostics.Where(d => d.Id == "NRDG011").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.Empty);
            Assert.That(GeneratedTable.ReadStringArray(source, "ExcludedActivitySourceAdditions"), Is.Empty);
            Assert.That(sites.Where(s => s.AssemblyName == "RabbitMQ.Client").Select(s => s.DefaultOff), Is.EqualTo(new[] { true, true, true }));
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "NRDG011" }));
            Assert.That(nrdg011.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(nrdg011.Single().GetMessage(), Is.EqualTo("OTel instrumentation for RabbitMQ.Client stays on: NR covers it only with default-off points (Off, Off2)"));
        });
    }

    [Test]
    public void Emit_DefaultOffAssemblyAlsoCoveredByDefaultOnPoints_DisablesAndReportsNoNrdg011()
    {
        var xml = Extension(@"<tracerFactory name=""Off"" enabled=""false""><match assemblyName=""RabbitMQ.Client"" className=""B"">"
            + Method("N", "System.Int32") + "</match></tracerFactory>"
            + Factory("On", @"<match assemblyName=""RabbitMQ.Client"" className=""B"">" + Method("M", "System.Int32") + "</match>"));

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, xml));
        var source = generatedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.EqualTo(new[] { "RABBITMQ" }));
        });
    }

    [TestCase(".NETCoreApp")]
    [TestCase(" .netcoreapp ")]
    public void Emit_NetCoreTarget_HttpClientXmlAlone_DisablesHttpClient(string targetFrameworkIdentifier)
    {
        var xml = Extension(Factory("F", @"<match assemblyName=""System.Net.Http"" className=""System.Net.Http.SocketsHttpHandler"">" + Method("SendAsync", "System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken") + "</match>"));

        var (diagnostics, generatedSources) = RunGeneratorForTargetFramework(targetFrameworkIdentifier, new XmlInput(SqlWrapperPath, xml));
        var source = generatedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.EqualTo(new[] { "HTTPCLIENT" }));
            Assert.That(GeneratedTable.ReadStringArray(source, "ExcludedActivitySourceAdditions"), Is.EqualTo(new[] { "OpenTelemetry.Instrumentation.Http.HttpClient", "OpenTelemetry.Instrumentation.Http.HttpWebRequest" }));
            Assert.That(source, Does.Contain("NetCore rules"));
        });
    }

    [TestCase(".NETFramework")]
    [TestCase(".NETStandard")]
    [TestCase("")]
    [TestCase(null)]
    public void Emit_NonNetCoreTarget_HttpClientXmlAlone_KeepsHttpClient(string targetFrameworkIdentifier)
    {
        var xml = Extension(Factory("F", @"<match assemblyName=""System.Net.Http"" className=""System.Net.Http.HttpClient"">" + Method("SendAsync", "System.Net.Http.HttpRequestMessage,System.Net.Http.HttpCompletionOption,System.Threading.CancellationToken") + "</match>"));

        var (diagnostics, generatedSources) = RunGeneratorForTargetFramework(targetFrameworkIdentifier, new XmlInput(SqlWrapperPath, xml));
        var source = generatedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.Empty);
            Assert.That(source, Does.Contain("NetFramework rules"));
        });
    }

    [Test]
    public void Emit_NetCoreTarget_DefaultOffHttpClientOnly_ReportsNrdg011()
    {
        // On .NET the HttpClient XML alone triggers HTTPCLIENT, so default-off HttpClient points alone leave OTel's on.
        var xml = Extension(@"<tracerFactory name=""Off"" enabled=""false""><match assemblyName=""System.Net.Http"" className=""B"">" + Method("M", "System.Int32") + "</match></tracerFactory>");

        var (netCoreDiagnostics, netCoreSources) = RunGeneratorForTargetFramework(".NETCoreApp", new XmlInput(SqlWrapperPath, xml));
        var (frameworkDiagnostics, _) = RunGeneratorForTargetFramework(".NETFramework", new XmlInput(SqlWrapperPath, xml));

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadStringArray(netCoreSources.Single().SourceText.ToString(), "DisabledOtelTraceInstrumentationIds"), Is.Empty);
            Assert.That(netCoreDiagnostics.Where(d => d.Id == "NRDG011").Select(d => d.GetMessage()), Is.EqualTo(new[]
            {
                "OTel instrumentation for System.Net.Http stays on: NR covers it only with default-off points (Off)"
            }));
            // On .NET Framework System.Net.Http alone never triggers the row, so there is no coverage to report.
            Assert.That(frameworkDiagnostics.Where(d => d.Id == "NRDG011"), Is.Empty);
        });
    }

    [Test]
    public void Emit_DefaultOffAssemblyCompletingAnAllOfRow_ReportsNrdg011()
    {
        // HTTPCLIENT needs both the HttpClient and the HttpWebRequest targets; a default-off point supplying one of them
        // would complete the row, which default-on points alone do not.
        var map = OtelInstrumentationMap.Rows.First(r => r.RequiredGroups.Count > 1 && r.OtelIds.Count > 0);
        var onAssembly = map.RequiredGroups[0][0];
        var offAssembly = map.RequiredGroups[1][0];
        var xml = Extension(
            Factory("On", @"<match assemblyName=""" + onAssembly + @""" className=""B"">" + Method("M", "System.Int32") + "</match>")
            + @"<tracerFactory name=""Off"" enabled=""false""><match assemblyName=""" + offAssembly + @""" className=""B"">" + Method("M", "System.Int32") + "</match></tracerFactory>");

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, xml));
        var source = generatedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadStringArray(source, "DisabledOtelTraceInstrumentationIds"), Is.EqualTo(OtelInstrumentationMap.Resolve(new[] { onAssembly }).disableIds));
            Assert.That(diagnostics.Where(d => d.Id == "NRDG011").Select(d => d.GetMessage()), Is.EqualTo(new[]
            {
                "OTel instrumentation for " + offAssembly + " stays on: NR covers it only with default-off points (Off)"
            }));
        });
    }

    [Test]
    public void Emit_PluginFile_SourceFileIsPlugin()
    {
        var xml = Extension(
            Factory("On", Match("", Method("M", "System.Int32")))
            + @"<tracerFactory name=""Off"" enabled=""false"">" + Match("", Method("N", "System.Int32")) + "</tracerFactory>");

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(PluginXmlPath, xml));
        var source = generatedSources.Single().SourceText.ToString();
        var (_, sites) = GeneratedTable.Read(source);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(sites.Select(s => s.SourceFile), Is.EqualTo(new[] { -1, -1 }));
            Assert.That(sites.Select(s => s.DefaultOff), Is.EqualTo(new[] { false, true }));
            Assert.That(GeneratedTable.ReadManifest(source).FileCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Emit_OverloadWideRecordsShareOnePointKey()
    {
        var xml = Extension(@"<tracerFactory name=""Off"" enabled=""false"">" + Match(@"minVersion=""2.0""", Method("M")) + "</tracerFactory>");

        var (_, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, xml));
        var (_, sites) = GeneratedTable.Read(generatedSources.Single().SourceText.ToString());
        var range = VersionRangeConverter.Convert("2.0", null);
        var expected = PointKeyHasher.Compute(new XmlInstrumentationPoint("Off", null, "A", "B", "M", null, "2.0", null, enabled: false), "B", range.RangeKey);

        Assert.Multiple(() =>
        {
            Assert.That(sites, Has.Count.EqualTo(IntegrationSourceEmitter.MaxParameters + 1));
            Assert.That(sites.Select(s => s.PointKey), Is.All.EqualTo(expected));
            Assert.That(sites.Select(s => s.DefaultOff), Is.All.True);
            Assert.That(sites.Select(s => s.FunctionId).Distinct().Count(), Is.EqualTo(IntegrationSourceEmitter.MaxParameters + 1));
        });
    }

    [Test]
    public void Emit_HashOnlyMetadataNotTrue_FileIsCompiled()
    {
        var xml = Extension(Factory("F", Match("", Method("M", "System.Int32"))));

        var (_, generatedSources) = RunGenerator(new XmlInput(SqlWrapperPath, xml, HashOnlyMetadata: "false"));
        var source = generatedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedTable.ReadDefinitions(source), Has.Count.EqualTo(1));
            Assert.That(GeneratedTable.ReadManifest(source).IsCompiled(0), Is.True);
        });
    }

    [Test]
    public void Emit_HashOnlyFileWithNoHomeName_IsIgnored()
    {
        var xml = Extension(Factory("F", Match("", Method("M", "System.Int32"))));

        var (diagnostics, generatedSources) = RunGenerator(new XmlInput(PluginXmlPath, xml, HashOnly: true));
        var source = generatedSources.Single().SourceText.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(GeneratedTable.ReadDefinitions(source), Is.Empty);
            Assert.That(GeneratedTable.ReadManifest(source).FileCount, Is.EqualTo(0));
        });
    }

    private static string Extension(string tracerFactories) => "<extension><instrumentation>" + tracerFactories + "</instrumentation></extension>";

    private static string Factory(string name, string matches) => @"<tracerFactory name=""" + name + @""">" + matches + "</tracerFactory>";

    private static string Match(string attributes, string methods) => @"<match assemblyName=""A"" className=""B"" " + attributes + ">" + methods + "</match>";

    private static string Method(string name, string parameters = null) => parameters == null
        ? @"<exactMethodMatcher methodName=""" + name + @""" />"
        : @"<exactMethodMatcher methodName=""" + name + @""" parameters=""" + parameters + @""" />";

    private static string IntegrationTypeName(string assembly, string type, string method, string argumentSignature, string rangeKey) =>
        DistroIntegrationGenerator.GeneratedNamespace + ".NrDistroIntegration_"
        + FunctionIdHasher.Compute(assembly, type, method, argumentSignature, rangeKey).ToString("X16", CultureInfo.InvariantCulture);

    /// <summary>Runs the generator over XML given as plugin files (the plugin's own Instrumentation folder), compiled.</summary>
    internal static (IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<GeneratedSourceResult> GeneratedSources) RunGenerator(params string[] xmlFiles) =>
        RunGenerator(xmlFiles.Select((xml, i) => new XmlInput("src/Agent/NewRelic/OtelDistro/NewRelic.OtelDistro.Plugin/Instrumentation/Test" + i + ".xml", xml)).ToArray());

    /// <summary>Runs the generator over XML files with their paths and per-file NrDistroHashOnly metadata.</summary>
    internal static (IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<GeneratedSourceResult> GeneratedSources) RunGenerator(params XmlInput[] xmlFiles) =>
        RunGeneratorWithGlobalOptions(new Dictionary<string, string>(), xmlFiles);

    /// <summary>Runs the generator over XML files built for a target framework identifier (null: the property is not set).</summary>
    internal static (IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<GeneratedSourceResult> GeneratedSources) RunGeneratorForTargetFramework(string targetFrameworkIdentifier, params XmlInput[] xmlFiles) =>
        RunGeneratorWithGlobalOptions(
            targetFrameworkIdentifier == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { [DistroIntegrationGenerator.TargetFrameworkIdentifierKey] = targetFrameworkIdentifier },
            xmlFiles);

    private static (IReadOnlyList<Diagnostic> Diagnostics, IReadOnlyList<GeneratedSourceResult> GeneratedSources) RunGeneratorWithGlobalOptions(Dictionary<string, string> globalOptions, XmlInput[] xmlFiles)
    {
        var additionalTexts = xmlFiles.Select(input => new StubAdditionalText(input.Path, input.Xml)).ToArray();
        var options = new Dictionary<AdditionalText, AnalyzerConfigOptions>();
        for (var i = 0; i < xmlFiles.Length; i++)
        {
            var metadata = xmlFiles[i].HashOnlyMetadata ?? (xmlFiles[i].HashOnly ? "true" : null);
            options.Add(additionalTexts[i], new StubOptions(metadata == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { [DistroIntegrationGenerator.HashOnlyMetadataKey] = metadata }));
        }

        var compilation = CSharpCompilation.Create(
            "GeneratorTestAssembly",
            Array.Empty<SyntaxTree>(),
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new DistroIntegrationGenerator().AsSourceGenerator() },
            additionalTexts,
            optionsProvider: new StubOptionsProvider(options, new StubOptions(globalOptions)));

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var runResult = driver.GetRunResult();

        var diagnostics = runResult.Results.SelectMany(r => r.Diagnostics).ToList();
        var generatedSources = runResult.Results.SelectMany(r => r.GeneratedSources).ToList();

        return (diagnostics, generatedSources);
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

    private sealed class StubAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public StubAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => _text;
    }

    /// <summary>One additional file: its path, content, and its NrDistroHashOnly metadata (HashOnly gives "true"; HashOnlyMetadata sets any raw value).</summary>
    internal sealed record XmlInput(string Path, string Xml, bool HashOnly = false, string HashOnlyMetadata = null);

    private sealed class StubOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public StubOptions(Dictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);
    }

    private sealed class StubOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly StubOptions Empty = new StubOptions(new Dictionary<string, string>());
        private readonly Dictionary<AdditionalText, AnalyzerConfigOptions> _byFile;

        public StubOptionsProvider(Dictionary<AdditionalText, AnalyzerConfigOptions> byFile, AnalyzerConfigOptions globalOptions)
        {
            _byFile = byFile;
            GlobalOptions = globalOptions;
        }

        public override AnalyzerConfigOptions GlobalOptions { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _byFile.TryGetValue(textFile, out var options) ? options : Empty;
    }
}
