// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using NewRelic.Agent.Core.OtelDistro;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

/// <summary>
/// Runs the generator over every shipped instrumentation XML file (the wrappers' plus the Misc file) and pins what the
/// version-range handling does with them: every shipped range converts exactly, and the only explicit/overload-wide
/// residue is MySql.Data's MySqlCommand.ExecuteNonQuery. A second run mirrors the plugin's default build (every shipped
/// file compiled from its home copy, and also listed hash-only from its source path -- the generator counts a file
/// listed both ways as compiled once, so this is equivalent to the first run) and pins its manifest, default-off points
/// and OTel disable list.
/// </summary>
[TestFixture]
public class ShippedInstrumentationXmlTests
{
    private List<string> _files;
    private IReadOnlyList<Diagnostic> _diagnostics;
    private IReadOnlyList<DistroCallTargetDefinition> _definitions;
    private IReadOnlyList<DistroInstrumentationSite> _sites;
    private IReadOnlyList<Diagnostic> _defaultBuildDiagnostics;
    private string _defaultBuildSource;
    private string _defaultBuildNetCoreSource;
    private IReadOnlyList<DistroInstrumentationSite> _defaultBuildSites;
    private DistroShippedXmlManifest _defaultBuildManifest;

    [OneTimeSetUp]
    public void RunGeneratorOverShippedXml()
    {
        var root = typeof(ShippedInstrumentationXmlTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "RepositoryRootDirectory").Value;
        var wrapperDirectory = Path.Combine(root, "src", "Agent", "NewRelic", "Agent", "Extensions", "Providers", "Wrapper");
        var separator = Path.DirectorySeparatorChar;
        _files = Directory.GetFiles(wrapperDirectory, "*.xml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(separator + "bin" + separator, StringComparison.OrdinalIgnoreCase) && !f.Contains(separator + "obj" + separator, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Append(Path.Combine(root, "src", "Agent", "Miscellaneous", "NewRelic.Providers.Wrapper.Misc.Instrumentation.xml"))
            .ToList();
        Assert.That(_files, Has.Count.GreaterThan(40), "shipped XML files found under " + wrapperDirectory);

        var (diagnostics, generatedSources) = DistroIntegrationGeneratorTests.RunGenerator(_files.Select(f => new DistroIntegrationGeneratorTests.XmlInput(f, File.ReadAllText(f))).ToArray());
        _diagnostics = diagnostics;
        (_definitions, _sites) = GeneratedTable.Read(generatedSources.Single().SourceText.ToString());

        // The plugin csproj's item set: every shipped file compiled (from its home copy), plus every shipped file
        // listed hash-only (from its source path). A file listed both ways is compiled once, so this is equivalent
        // to compiling every file outright -- which is what the two defaultBuild* sources below pin.
        var defaultBuild = _files.Select(f => new DistroIntegrationGeneratorTests.XmlInput(f, File.ReadAllText(f)))
            .Concat(_files.Select(f => new DistroIntegrationGeneratorTests.XmlInput(f, File.ReadAllText(f), HashOnly: true)))
            .ToArray();
        var (defaultDiagnostics, defaultSources) = DistroIntegrationGeneratorTests.RunGenerator(defaultBuild);
        _defaultBuildDiagnostics = defaultDiagnostics;
        _defaultBuildSource = defaultSources.Single().SourceText.ToString();
        _defaultBuildSites = GeneratedTable.Read(_defaultBuildSource).Sites;
        _defaultBuildManifest = GeneratedTable.ReadManifest(_defaultBuildSource);
        _defaultBuildNetCoreSource = DistroIntegrationGeneratorTests.RunGeneratorForTargetFramework(".NETCoreApp", defaultBuild).GeneratedSources.Single().SourceText.ToString();
    }

    [Test]
    public void ShippedXml_EveryRangeConvertsExactlyWithNoSameKeyConflicts()
    {
        Assert.That(_diagnostics.Where(d => d.Id is "NRDG006" or "NRDG007" or "NRDG008" or "NRDG010").Select(d => d.Id + ": " + d.GetMessage()), Is.Empty);
    }

    [Test]
    public void ShippedXml_ExplicitOverloadWideResiduesAreMySqlExecuteNonQueryAndTheDefaultOffDataReaderMethods()
    {
        // ExecuteNonQuery is default-on. NextResult and Read come from the Sql XML's default-off DataReaderTracer factory
        // (overload-wide up to 8.0.33, explicit with a CancellationToken from 9.7.0), which is emitted since default-off
        // points reach the table; the message says so.
        var residues = _diagnostics.Where(d => d.Id == "NRDG009").Select(d => d.GetMessage()).ToList();

        Assert.That(residues, Has.Count.EqualTo(3));
        Assert.That(residues, Has.One.StartsWith("The overload-wide match for MySql.Data/MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQuery "));
        Assert.That(residues, Has.One.StartsWith("The default-off overload-wide match for MySql.Data/MySql.Data.MySqlClient.MySqlDataReader.NextResult "));
        Assert.That(residues, Has.One.StartsWith("The default-off overload-wide match for MySql.Data/MySql.Data.MySqlClient.MySqlDataReader.Read "));
        Assert.That(_defaultBuildDiagnostics.Where(d => d.Id == "NRDG009").Select(d => d.GetMessage()), Is.EquivalentTo(residues));
    }

    [Test]
    public void ShippedXml_OnlyDuplicateIsWebApiInvokeActionAsync()
    {
        // The Sql XML's two default-off factories, DataReaderTracer and DataReaderTracerAsync, share Npgsql matches; both
        // are kept (different factories, default-off), so the only duplicate is the default-on Web API one. The default
        // build compiles every shipped file (same as the full run above), so it reports the same duplicate.
        var duplicates = _diagnostics.Where(d => d.Id == "NRDG003").Select(d => d.GetMessage().Substring(0, d.GetMessage().IndexOf(" duplicates", StringComparison.Ordinal))).ToList();
        var defaultBuildDuplicates = _defaultBuildDiagnostics.Where(d => d.Id == "NRDG003").Select(d => d.GetMessage().Substring(0, d.GetMessage().IndexOf(" duplicates", StringComparison.Ordinal))).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(duplicates, Is.EqualTo(new[] { "The match for System.Web.Http/System.Web.Http.Controllers.ApiControllerActionInvoker.InvokeActionAsync" }));
            Assert.That(defaultBuildDuplicates, Is.EqualTo(duplicates));
        });
    }

    [Test]
    public void ShippedXml_EveryParameterTypeHasBalancedBrackets()
    {
        // A parameters attribute split inside a generic instance (Name`N[A,B]) yields pieces with unbalanced brackets and
        // a wrong parameter count, and OTel's native matcher then skips the method ("doesn't have the right number of
        // arguments"). MEL Logger.Log, Kafka Produce and the ServiceBus settle calls carry such parameters.
        var unbalanced = _files
            .SelectMany(path => InstrumentationXmlParser.Parse(File.ReadAllText(path)).Points)
            .Where(p => p.ParameterTypes != null)
            .SelectMany(p => p.ParameterTypes.Select(t => (Point: p, Type: t)))
            .Where(x => x.Type.Count(c => c == '[') != x.Type.Count(c => c == ']'))
            .Select(x => x.Point.AssemblyName + "/" + x.Point.ClassName + "." + x.Point.MethodName + ": " + x.Type)
            .ToList();

        Assert.That(unbalanced, Is.Empty);
    }

    [Test]
    public void ShippedXml_MicrosoftExtensionsLoggingLogPointHasFiveParameters()
    {
        var log = _files
            .SelectMany(path => InstrumentationXmlParser.Parse(File.ReadAllText(path)).Points)
            .Single(p => p.ClassName == "Microsoft.Extensions.Logging.Logger" && p.MethodName == "Log");

        Assert.That(log.ParameterTypes, Has.Count.EqualTo(5));
    }

    [Test]
    public void ShippedXml_DefaultBuild_EveryDefaultOffPointOfTheCompiledXmlIsInTheTable()
    {
        // Turning any disabled factory on in a compiled shipped file must find its points in the table: every default-off
        // point the generator can emit (class name usable, at most 8 parameters, non-empty range) has its point key there.
        // The default build compiles every shipped file, so this checks every shipped file's default-off points (today
        // only Sql's XML has enabled="false" factories; see ShippedXml_DefaultBuild_SqlDataReaderPointsAreDefaultOff).
        var tableKeys = new HashSet<ulong>(_defaultBuildSites.Select(s => s.PointKey));
        var expected = _files
            .SelectMany(path => InstrumentationXmlParser.Parse(File.ReadAllText(path)).Points)
            .Where(p => !p.Enabled && (p.ClassName == null || (p.ClassName.Length > 0 && p.ClassName.Trim().Length == p.ClassName.Length)))
            .Where(p => p.ParameterTypes == null || p.ParameterTypes.Count <= IntegrationSourceEmitter.MaxParameters)
            .Select(p => (Point: p, Range: VersionRangeConverter.Convert(p.MinVersion, p.MaxVersion)))
            .Where(p => !p.Range.IsEmpty)
            .ToList();
        var missing = expected
            .Where(p => !tableKeys.Contains(PointKeyHasher.Compute(p.Point, p.Point.ClassName, p.Range.RangeKey)))
            .Select(p => p.Point.TracerFactoryName + ": " + p.Point.AssemblyName + "/" + p.Point.ClassName + "." + p.Point.MethodName)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(expected.Select(p => p.Point.TracerFactoryName).Distinct(), Is.EquivalentTo(new[] { "DataReaderTracer", "DataReaderTracerAsync" }));
            Assert.That(missing, Is.Empty);
        });
    }

    [Test]
    public void ShippedXml_AspNetCoreBuildCommonServicesSiblingsAreBothPresent()
    {
        var ranges = _definitions
            .Where(d => d.TargetAssembly == "Microsoft.AspNetCore.Hosting" && d.TargetType == "Microsoft.AspNetCore.Hosting.WebHostBuilder" && d.TargetMethod == "BuildCommonServices")
            .Select(d => $"{d.MinMajor}.{d.MinMinor}.{d.MinPatch}-{d.MaxMajor}.{d.MaxMinor}.{d.MaxPatch}")
            .ToList();

        // Overload-wide matches, so nine arity variants per range.
        Assert.That(ranges.GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count()), Is.EqualTo(new Dictionary<string, int>
        {
            ["1.0.0-5.65535.65535"] = 9,
            ["6.0.0-65535.65535.65535"] = 9,
        }));
    }

    [Test]
    public void ShippedXml_NoDefaultOffOnlyCoverage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_diagnostics.Where(d => d.Id == "NRDG011").Select(d => d.GetMessage()), Is.Empty);
            Assert.That(_defaultBuildDiagnostics.Where(d => d.Id == "NRDG011").Select(d => d.GetMessage()), Is.Empty);
        });
    }

    [Test]
    public void ShippedXml_DefaultBuild_SqlDataReaderPointsAreDefaultOff()
    {
        var dataReaderFactories = new[] { "DataReaderTracer", "DataReaderTracerAsync" };
        var off = _defaultBuildSites.Where(s => s.DefaultOff).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(off, Is.Not.Empty);
            Assert.That(off.Select(s => s.TracerFactoryName).Distinct(), Is.EquivalentTo(dataReaderFactories));
            Assert.That(_defaultBuildSites.Where(s => dataReaderFactories.Contains(s.TracerFactoryName)).Select(s => s.DefaultOff), Is.All.True);
            Assert.That(off.Select(s => _defaultBuildManifest.HomeName(s.SourceFile)).Distinct(), Is.EqualTo(new[] { "NewRelic.Providers.Wrapper.Sql.Instrumentation.xml" }));
        });
    }

    [Test]
    public void ShippedXml_DefaultBuild_DisableList()
    {
        // Every shipped file is compiled by default now, so the disable list covers every shipped library the
        // OTel instrumentation map (OtelInstrumentationMap) has a .NET Framework row for, not just a hand-picked subset.
        Assert.That(GeneratedTable.ReadStringArray(_defaultBuildSource, "DisabledOtelTraceInstrumentationIds"),
            Is.EqualTo(new[]
            {
                "ADONET", "ASPNET", "ASPNETCORE", "HTTPCLIENT", "KAFKA", "MASSTRANSIT", "MONGODB", "MYSQLCONNECTOR",
                "MYSQLDATA", "NPGSQL", "NSERVICEBUS", "ORACLEMDA", "RABBITMQ", "SQLCLIENT", "STACKEXCHANGEREDIS",
                "WCFCLIENT", "WCFSERVICE",
            }));
    }

    [Test]
    public void ShippedXml_DefaultBuild_NetCore_DisableListUnchanged()
    {
        // This fixture's defaultBuild compiles every shipped file for both the .NET Framework-identifier run above and
        // this .NETCoreApp-identifier one -- unlike the real plugin build, which feeds each target framework only the
        // files its own agent home ships (so there, e.g., the .NET Framework-only AspNet/Wcf* wrapper XML is absent from
        // the .NET Core input and their ASPNET/WCFCLIENT/WCFSERVICE rows never fire there). HTTPCLIENT is the only
        // OtelInstrumentationMap row with its own .NET (Core/5+) required groups (HttpClient alone, vs. HttpClient AND
        // HttpWebRequest on .NET Framework); both groups are present in the full shipped set either way, so with every
        // shipped file compiled for both runs, the two runs' disable lists come out identical.
        Assert.That(GeneratedTable.ReadStringArray(_defaultBuildNetCoreSource, "DisabledOtelTraceInstrumentationIds"),
            Is.EqualTo(GeneratedTable.ReadStringArray(_defaultBuildSource, "DisabledOtelTraceInstrumentationIds")));
    }

    [Test]
    public void ShippedXml_DefaultBuild_LogForwardingSitesAreExactlyTheFiveLogRecordingWrappers()
    {
        // The plugin brackets a log-forwarding site with the OTel/NR log dedupe counter. Across the whole shipped set the
        // flagged sites must be exactly NR's log-recording wrappers (MEL Logger.Log, log4net/Sitecore CallAppenders,
        // Serilog's dispatch, NLog LoggerImpl.Write) -- the five tracer factories in LogForwardingClassification -- and
        // nothing else. In particular the logger-wiring factories that ship alongside them are present but NOT flagged.
        var flaggedFactories = _defaultBuildSites.Where(s => s.LogForwarding).Select(s => s.TracerFactoryName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        var flaggedMethods = _defaultBuildSites.Where(s => s.LogForwarding).Select(s => s.MethodName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        var unflaggedWiringFactories = _defaultBuildSites.Where(s => !s.LogForwarding).Select(s => s.TracerFactoryName).Distinct().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(flaggedFactories, Is.EqualTo(new[] { "MicrosoftLogging", "SerilogDispatchWrapper", "SitecoreLogging", "log4net", "nlog" }));
            Assert.That(flaggedMethods, Is.EqualTo(new[] { "CallAppenders", "Dispatch", "Log", "PostLevelCheckEmit", "Write" }));
            // Logger creation/registration ships alongside the recording methods and must stay unflagged.
            Assert.That(unflaggedWiringFactories, Does.Contain("AddProviderRegistrationWrapper"));
            Assert.That(unflaggedWiringFactories, Does.Contain("SerilogCreateLoggerWrapper"));
            Assert.That(unflaggedWiringFactories, Does.Not.Contain("MicrosoftLogging"));
            // Every site of a log-forwarding tracer factory is flagged (no half-flagged factory).
            Assert.That(_defaultBuildSites.Where(s => flaggedFactories.Contains(s.TracerFactoryName)).All(s => s.LogForwarding), Is.True);
        });
    }

    [Test]
    public void ShippedXml_DefaultBuild_NetCore_LogForwardingSitesUnchanged()
    {
        // The log-forwarding flag is runtime-independent (it is judged by tracer-factory name), and every logging wrapper
        // ships on both runtimes, so the .NETCoreApp run flags the same sites as the .NET Framework run.
        var netCoreSites = GeneratedTable.Read(_defaultBuildNetCoreSource).Sites;
        var netCoreFlagged = netCoreSites.Where(s => s.LogForwarding).Select(s => s.TracerFactoryName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        var frameworkFlagged = _defaultBuildSites.Where(s => s.LogForwarding).Select(s => s.TracerFactoryName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.That(netCoreFlagged, Is.EqualTo(frameworkFlagged));
    }

    [Test]
    public void ShippedXml_DefaultBuild_FlagsNrCompilesNLogLogForwarding()
    {
        // NR's NLog wrapper is compiled default-on, so the plugin can diagnose a customer who also turns OTel's opt-in
        // NLog bridge on (duplicate NLog lines). Emitted as a const bool; both runtimes ship the NLog wrapper.
        Assert.Multiple(() =>
        {
            Assert.That(_defaultBuildSource, Does.Contain("public const bool NrCompilesNLogLogForwarding = true;"));
            Assert.That(_defaultBuildNetCoreSource, Does.Contain("public const bool NrCompilesNLogLogForwarding = true;"));
        });
    }

    [Test]
    public void ShippedXml_DefaultBuild_ManifestHasEveryShippedFileCompiled()
    {
        var expectedNames = _files.Select(f => ShippedXmlFileNames.TryGetHomeName(f, out var name) ? name : "(none) " + f).ToList();
        var names = Enumerable.Range(0, _defaultBuildManifest.FileCount).Select(_defaultBuildManifest.HomeName).ToList();
        var compiled = Enumerable.Range(0, _defaultBuildManifest.FileCount).Where(_defaultBuildManifest.IsCompiled).Select(_defaultBuildManifest.HomeName).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(names, Is.EquivalentTo(expectedNames));
            Assert.That(names, Has.Member("NewRelic.Providers.Wrapper.Misc.Instrumentation.xml"));
            Assert.That(names, Has.Member("NewRelic.Providers.Wrapper.Kafka.Instrumentation.xml"));
            // Every shipped file is compiled by default now (home copy compiled + source path hash-only -> compiled wins).
            Assert.That(compiled, Is.EquivalentTo(names));
            for (var file = 0; file < _defaultBuildManifest.FileCount; file++)
            {
                var path = _files[expectedNames.IndexOf(_defaultBuildManifest.HomeName(file))];
                Assert.That(_defaultBuildManifest.ContentHash(file), Is.EqualTo(XmlContentHasher.Compute(File.ReadAllText(path))), path);

                // The run time hashes the raw bytes (no decode); for every shipped file that must give the build's value.
                Assert.That(XmlContentHasher.Compute(File.ReadAllBytes(path)), Is.EqualTo(_defaultBuildManifest.ContentHash(file)), "bytes: " + path);
            }
        });
    }
}
