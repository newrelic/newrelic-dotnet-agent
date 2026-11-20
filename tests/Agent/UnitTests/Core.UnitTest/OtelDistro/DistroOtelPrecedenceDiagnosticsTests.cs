// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.OtelDistro.PrecedenceTestTypes;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroOtelPrecedenceDiagnosticsTests
{
    private const string Metric = "Supportability/Dotnet/OtelDistro/Precedence/OtelWins";
    private const string Ns = "NewRelic.Agent.Core.OtelDistro.PrecedenceTestTypes.";

    private static readonly string TestAssembly = typeof(PrecedenceDerived).Assembly.GetName().Name;

    private DistroRecordingLogger _logger;
    private List<string> _metrics;

    [SetUp]
    public void SetUp()
    {
        DistroOtelPrecedenceDiagnostics.ResetForTesting();
        DistroConfigMetrics.ResetForTesting();
        _metrics = new List<string>();
        DistroConfigMetrics.SetMetricSink(_metrics.Add);
        _logger = new DistroRecordingLogger();
        Log.Initialize(_logger);
    }

    [TearDown]
    public void TearDown()
    {
        Log.Initialize(new NoOpLogger());
        DistroConfigMetrics.ResetForTesting();
        DistroOtelPrecedenceDiagnostics.ResetForTesting();
    }

    private static OtelSite Otel(string type = "MySql.Data.MySqlClient.MySqlCommand", string method = "ExecuteNonQuery", string[] signature = null, bool derived = false, string assembly = "MySql.Data") => new OtelSite
    {
        Id = "MYSQLDATA",
        Integration = "MySqlData",
        Assembly = assembly,
        Type = type,
        Method = method,
        Signature = signature ?? new string[0],
        MatchesDerivedOrInterface = derived,
    };

    private static NrSite Nr(string type = "MySql.Data.MySqlClient.MySqlCommand", string method = "ExecuteNonQuery", string[] signature = null, string assembly = "MySql.Data", string tracerFactory = "SqlCommandTracerFactory") => new NrSite
    {
        TracerFactory = tracerFactory,
        Assembly = assembly,
        Type = type,
        Method = method,
        Signature = signature,
    };

    private int LogCount() =>
        new[] { Level.Error, Level.Warn, Level.Info, Level.Debug, Level.Finest }.Sum(l => _logger.Messages(l).Length);

    // ---- Report: exact (Direct) sites ----

    [Test]
    public void Report_ExactSignatureOverlap_OneWarningAndOneMetric()
    {
        var otel = Otel(signature: new[] { "System.Data.CommandBehavior" }, method: "ExecuteReader");
        var nr = Nr(signature: new[] { "System.Data.CommandBehavior" }, method: "ExecuteReader");

        DistroOtelPrecedenceDiagnostics.Report(new[] { otel }, new[] { nr });

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "OTel instrumentation MYSQLDATA (MySqlData) takes precedence over NR SqlCommandTracerFactory on MySql.Data.MySqlClient.MySqlCommand.ExecuteReader(System.Data.CommandBehavior); NR instrumentation will not run there." }));
            Assert.That(LogCount(), Is.EqualTo(1));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void Report_ParameterlessOverlap_PrintsEmptySignature()
    {
        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, new[] { Nr(signature: new string[0]) });

        Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "OTel instrumentation MYSQLDATA (MySqlData) takes precedence over NR SqlCommandTracerFactory on MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQuery(); NR instrumentation will not run there." }));
    }

    [Test]
    public void Report_SignaturesDifferOnlyInCaseAndWhitespace_StillOverlap()
    {
        DistroOtelPrecedenceDiagnostics.Report(
            new[] { Otel(signature: new[] { "System.Data.CommandBehavior", "System.Threading.CancellationToken" }) },
            new[] { Nr(signature: new[] { " system.data.commandbehavior", "System.Threading. CancellationToken " }) });

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void Report_SameMethodDifferentSignature_ReportsNothing()
    {
        DistroOtelPrecedenceDiagnostics.Report(
            new[] { Otel(signature: new[] { "System.Data.CommandBehavior" }), Otel(signature: new[] { "System.Int32", "System.Int32" }) },
            new[] { Nr(signature: new[] { "System.Int32" }) });

        Assert.Multiple(() =>
        {
            Assert.That(LogCount(), Is.Zero);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void Report_NrOverloadWide_MatchesEachOtelOverloadOnce()
    {
        DistroOtelPrecedenceDiagnostics.Report(
            new[] { Otel(signature: new[] { "System.Data.CommandBehavior" }), Otel() },
            new[] { Nr(signature: null) });

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[]
            {
                "OTel instrumentation MYSQLDATA (MySqlData) takes precedence over NR SqlCommandTracerFactory on MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQuery(System.Data.CommandBehavior); NR instrumentation will not run there.",
                "OTel instrumentation MYSQLDATA (MySqlData) takes precedence over NR SqlCommandTracerFactory on MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQuery(); NR instrumentation will not run there.",
            }));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric, Metric }));
        });
    }

    [TestCase("Other.Assembly", "MySql.Data.MySqlClient.MySqlCommand", "ExecuteNonQuery")]
    [TestCase("MySql.Data", "MySql.Data.MySqlClient.MySqlConnection", "ExecuteNonQuery")]
    [TestCase("MySql.Data", "MySql.Data.MySqlClient.MySqlCommand", "ExecuteScalar")]
    [TestCase("MySql.Data", "mysql.data.mysqlclient.mysqlcommand", "ExecuteNonQuery")]
    [TestCase("MySql.Data", "MySql.Data.MySqlClient.MySqlCommand", "executenonquery")]
    public void Report_DifferentAssemblyTypeOrMethod_ReportsNothing(string assembly, string type, string method)
    {
        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, new[] { Nr(type: type, method: method, assembly: assembly, signature: new string[0]) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void Report_AssemblyNameCaseInsensitive_Overlaps()
    {
        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, new[] { Nr(assembly: "mysql.data", signature: new string[0]) });

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void Report_OtelNullSignature_TreatedAsParameterless()
    {
        var otel = Otel();
        otel.Signature = null;

        DistroOtelPrecedenceDiagnostics.Report(new[] { otel }, new[] { Nr(signature: new string[0]) });

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void Report_DerivedOtelSite_IsLeftToReportDerived()
    {
        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel(derived: true) }, new[] { Nr(signature: new string[0]) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void Report_SameMethodTwice_OneLine()
    {
        var otel = new[] { Otel(), Otel() };
        var nr = new[] { Nr(signature: new string[0]), Nr(signature: null, tracerFactory: "OtherTracerFactory") };

        DistroOtelPrecedenceDiagnostics.Report(otel, nr);
        DistroOtelPrecedenceDiagnostics.Report(otel, nr);

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Has.Length.EqualTo(1));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void Report_EmptyOrNullInputs_ReportNothing()
    {
        DistroOtelPrecedenceDiagnostics.Report(new OtelSite[0], new NrSite[0]);
        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, new NrSite[0]);
        DistroOtelPrecedenceDiagnostics.Report(new OtelSite[0], new[] { Nr() });
        DistroOtelPrecedenceDiagnostics.Report(null, new[] { Nr() });
        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, null);
        DistroOtelPrecedenceDiagnostics.Report(new OtelSite[] { null }, new NrSite[] { null });
        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, new NrSite[] { null });

        Assert.Multiple(() =>
        {
            Assert.That(LogCount(), Is.Zero);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void Report_BeforeSinkSet_MetricQueuedThenFlushed()
    {
        DistroConfigMetrics.ResetForTesting();

        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, new[] { Nr(signature: new string[0]) });
        Assert.That(_metrics, Is.Empty);

        DistroConfigMetrics.SetMetricSink(_metrics.Add);

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void Report_LoggerThrows_DoesNotThrowAndStillRecordsEveryMetric()
    {
        Log.Initialize(new ThrowingLogger());

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => DistroOtelPrecedenceDiagnostics.Report(
                new[] { Otel(), Otel(method: "ExecuteScalar") },
                new[] { Nr(signature: null), Nr(method: "ExecuteScalar", signature: null) }));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric, Metric }));
        });
    }

    [Test]
    public void Report_EnumerationThrows_DoesNotThrow()
    {
        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => DistroOtelPrecedenceDiagnostics.Report(new ThrowingEnumerable<OtelSite>(), new[] { Nr() }));
            Assert.DoesNotThrow(() => DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, new ThrowingEnumerable<NrSite>()));
        });
    }

    [Test]
    public void ResetForTesting_ForgetsReportedMethods()
    {
        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, new[] { Nr(signature: null) });
        DistroOtelPrecedenceDiagnostics.ResetForTesting();
        DistroOtelPrecedenceDiagnostics.Report(new[] { Otel() }, new[] { Nr(signature: null) });

        Assert.That(_metrics, Is.EqualTo(new[] { Metric, Metric }));
    }

    // ---- ReportDerived: Derived / Interface sites against a resolved NR target type ----

    private static OtelSite DerivedOtel(string baseType, string method = "Run", string[] signature = null, string assembly = null) =>
        Otel(type: Ns + baseType, method: method, signature: signature ?? new[] { "System.String" }, derived: true, assembly: assembly ?? TestAssembly);

    private static NrSite NrOn(Type type, string method = "Run", string[] signature = null) =>
        Nr(type: type.FullName, method: method, signature: signature ?? new[] { "System.String" }, assembly: TestAssembly);

    [Test]
    public void ReportDerived_TypeWithThatBase_OneWarning()
    {
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new[] { DerivedOtel("PrecedenceBase") }, new[] { NrOn(typeof(PrecedenceDerived)) });

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "OTel instrumentation MYSQLDATA (MySqlData) takes precedence over NR SqlCommandTracerFactory on " + Ns + "PrecedenceDerived.Run(System.String); NR instrumentation will not run there." }));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void ReportDerived_BaseFurtherUpTheChain_ReportsNothing()
    {
        // OTel native compares the immediate base type only (rejit_preprocessor.cpp maxDepth = 1).
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceGrandchild), new[] { DerivedOtel("PrecedenceBase") }, new[] { NrOn(typeof(PrecedenceGrandchild)) });

        Assert.Multiple(() =>
        {
            Assert.That(LogCount(), Is.Zero);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void ReportDerived_InterfaceInheritedFromBase_ReportsNothing()
    {
        // OTel native compares only the interfaces on the type's own InterfaceImpl rows.
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceInheritsInterface), new[] { DerivedOtel("IPrecedenceTarget") }, new[] { NrOn(typeof(PrecedenceInheritsInterface)) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void ReportDerived_InterfaceItself_ReportsNothing()
    {
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(IPrecedenceTarget), new[] { DerivedOtel("IPrecedenceTarget") }, new[] { NrOn(typeof(IPrecedenceTarget)) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void ReportDerived_OnlyOneOtelOverloadDeclaredOnTheType_OneWarningAndOneMetric()
    {
        // RabbitMQ shape: OTel has v5 and v6 overload sites, the loaded type declares only one; the NR site matches every overload.
        var otel = new[] { DerivedOtel("PrecedenceBase"), DerivedOtel("PrecedenceBase", signature: new[] { "System.ReadOnlyMemory`1[System.Byte]" }) };
        var nr = new[] { Nr(type: typeof(PrecedenceDerived).FullName, method: "Run", assembly: TestAssembly) };

        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), otel, nr);

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "OTel instrumentation MYSQLDATA (MySqlData) takes precedence over NR SqlCommandTracerFactory on " + Ns + "PrecedenceDerived.Run(System.String); NR instrumentation will not run there." }));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void ReportDerived_ParameterShapes_ComparedInOtelSpelling()
    {
        var shapes = typeof(PrecedenceShapes<>);
        var signature = new[] { "System.Int32&", "!0[]", "System.Collections.Generic.List`1[!0]", "!!0", "System.Int32[,]" };

        DistroOtelPrecedenceDiagnostics.ReportDerived(shapes, new[] { DerivedOtel("PrecedenceBase", method: "Shape", signature: signature) }, new[] { Nr(type: shapes.FullName, method: "Shape", assembly: TestAssembly) });

        Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "OTel instrumentation MYSQLDATA (MySqlData) takes precedence over NR SqlCommandTracerFactory on " + Ns + "PrecedenceShapes`1.Shape(System.Int32&,!0[],System.Collections.Generic.List`1[!0],!!0,System.Int32[,]); NR instrumentation will not run there." }));
    }

    [Test]
    public void ReportDerived_DeclaredOverrideButNrSiteOnAnotherMethodOrOverload_ReportsNothing()
    {
        DistroOtelPrecedenceDiagnostics.ReportDerived(
            typeof(PrecedenceDerived),
            new[] { DerivedOtel("PrecedenceBase") },
            new[] { NrOn(typeof(PrecedenceDerived), method: "Walk", signature: new string[0]), NrOn(typeof(PrecedenceDerived), signature: new[] { "System.Int32" }) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void ReportDerived_OtelNullSignature_MatchesDeclaredParameterlessMethod()
    {
        var otel = DerivedOtel("PrecedenceBase", method: "Walk");
        otel.Signature = null;

        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new[] { otel }, new[] { NrOn(typeof(PrecedenceDerived), method: "Walk", signature: new string[0]) });

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void ReportDerived_TypeImplementingTheInterface_OneWarning()
    {
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceImplementation), new[] { DerivedOtel("IPrecedenceTarget") }, new[] { NrOn(typeof(PrecedenceImplementation)) });

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void ReportDerived_NrOverloadWide_MatchesOtelSignature()
    {
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new[] { DerivedOtel("PrecedenceBase") }, new[] { Nr(type: typeof(PrecedenceDerived).FullName, method: "Run", assembly: TestAssembly) });

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void ReportDerived_TheOtelTargetTypeItself_ReportsNothing()
    {
        // OTel's Derived/Interface sites rewrite the overrides in derived types, not the base declaration.
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceBase), new[] { DerivedOtel("PrecedenceBase") }, new[] { NrOn(typeof(PrecedenceBase)) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void ReportDerived_UnrelatedType_ReportsNothing()
    {
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceUnrelated), new[] { DerivedOtel("PrecedenceBase"), DerivedOtel("IPrecedenceTarget") }, new[] { NrOn(typeof(PrecedenceUnrelated)) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void ReportDerived_BaseFromAnotherAssemblyName_ReportsNothing()
    {
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new[] { DerivedOtel("PrecedenceBase", assembly: "Some.Other.Assembly") }, new[] { NrOn(typeof(PrecedenceDerived)) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void ReportDerived_DifferentMethodOrSignature_ReportsNothing()
    {
        DistroOtelPrecedenceDiagnostics.ReportDerived(
            typeof(PrecedenceDerived),
            new[] { DerivedOtel("PrecedenceBase", method: "Walk"), DerivedOtel("PrecedenceBase", signature: new[] { "System.Int32" }) },
            new[] { NrOn(typeof(PrecedenceDerived)) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void ReportDerived_NrSiteOnAnotherTypeOrAssembly_ReportsNothing()
    {
        var otherAssembly = NrOn(typeof(PrecedenceDerived));
        otherAssembly.Assembly = "Some.Other.Assembly";

        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new[] { DerivedOtel("PrecedenceBase") }, new[] { NrOn(typeof(PrecedenceGrandchild)), otherAssembly });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void ReportDerived_DirectOtelSite_IsLeftToReport()
    {
        var direct = DerivedOtel("PrecedenceBase");
        direct.MatchesDerivedOrInterface = false;

        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new[] { direct }, new[] { NrOn(typeof(PrecedenceDerived)) });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void ReportDerived_SameMethodTwice_OneLine()
    {
        var otel = new[] { DerivedOtel("PrecedenceBase"), DerivedOtel("IPrecedenceTarget") };
        var nr = new[] { NrOn(typeof(PrecedenceBoth)) };

        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceBoth), otel, nr);
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceBoth), otel, nr);

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Has.Length.EqualTo(1));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void ReportDerived_EmptyOrNullInputs_ReportNothing()
    {
        DistroOtelPrecedenceDiagnostics.ReportDerived(null, new[] { DerivedOtel("PrecedenceBase") }, new[] { NrOn(typeof(PrecedenceDerived)) });
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), null, new[] { NrOn(typeof(PrecedenceDerived)) });
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new[] { DerivedOtel("PrecedenceBase") }, null);
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new OtelSite[] { null }, new NrSite[] { null });
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new[] { DerivedOtel("PrecedenceBase") }, new NrSite[] { null });
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new OtelSite[] { null }, new[] { NrOn(typeof(PrecedenceDerived)) });
        DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new OtelSite[0], new NrSite[0]);

        Assert.Multiple(() =>
        {
            Assert.That(LogCount(), Is.Zero);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void ReportDerived_EnumerationThrows_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => DistroOtelPrecedenceDiagnostics.ReportDerived(typeof(PrecedenceDerived), new[] { DerivedOtel("PrecedenceBase") }, new ThrowingEnumerable<NrSite>()));
    }

    // ---- ParseSignature / NrSite.From ----

    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("   ", null)]
    [TestCase("void", new string[0])]
    [TestCase(" VOID ", new string[0])]
    [TestCase("System.String", new[] { "System.String" })]
    [TestCase("System.Data.CommandBehavior, System.Threading.CancellationToken", new[] { "System.Data.CommandBehavior", "System.Threading.CancellationToken" })]
    [TestCase("System.Collections.Generic.Dictionary`2[System.String,System.Int32],System.Int32", new[] { "System.Collections.Generic.Dictionary`2[System.String,System.Int32]", "System.Int32" })]
    [TestCase("System.Func`2<System.String,System.Int32>,System.Int32[,]", new[] { "System.Func`2<System.String,System.Int32>", "System.Int32[,]" })]
    public void ParseSignature_SplitsTopLevelParameters(string signature, string[] expected)
    {
        Assert.That(DistroOtelPrecedenceDiagnostics.ParseSignature(signature), Is.EqualTo(expected));
    }

    [Test]
    public void NrSiteFrom_CopiesTheSiteAndParsesItsSignature()
    {
        var site = new DistroInstrumentationSite("SqlCommandTracerFactory", "metric", 0, "MySql.Data", "MySql.Data.MySqlClient.MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior", 7);

        var nr = NrSite.From(site);

        Assert.Multiple(() =>
        {
            Assert.That(nr.TracerFactory, Is.EqualTo("SqlCommandTracerFactory"));
            Assert.That(nr.Assembly, Is.EqualTo("MySql.Data"));
            Assert.That(nr.Type, Is.EqualTo("MySql.Data.MySqlClient.MySqlCommand"));
            Assert.That(nr.Method, Is.EqualTo("ExecuteReader"));
            Assert.That(nr.Signature, Is.EqualTo(new[] { "System.Data.CommandBehavior" }));
        });
    }

    [Test]
    public void NrSiteFrom_OverloadWideSite_NullSignature()
    {
        var nr = NrSite.From(new DistroInstrumentationSite("F", null, 0, "A", "T", "M", null, 0));

        Assert.That(nr.Signature, Is.Null);
    }

    // ---- DistroOtelDerivedPrecedenceWatcher ----

    [Test]
    public void Watcher_AssemblyWithNrTargetDerivingFromOtelSite_OneWarning()
    {
        var watcher = new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel("PrecedenceBase"), Otel() }, new[] { NrOn(typeof(PrecedenceDerived)), Nr() });

        watcher.OnAssemblyLoaded(typeof(PrecedenceDerived).Assembly);
        watcher.OnAssemblyLoaded(typeof(PrecedenceDerived).Assembly);

        Assert.Multiple(() =>
        {
            Assert.That(watcher.HasWork, Is.True);
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "OTel instrumentation MYSQLDATA (MySqlData) takes precedence over NR SqlCommandTracerFactory on " + Ns + "PrecedenceDerived.Run(System.String); NR instrumentation will not run there." }));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void Watcher_AssemblyWithoutNrSites_ReportsNothing()
    {
        var watcher = new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel("PrecedenceBase") }, new[] { Nr() });

        watcher.OnAssemblyLoaded(typeof(PrecedenceDerived).Assembly);

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void Watcher_NrTypeNotInAssembly_ReportsNothing()
    {
        var watcher = new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel("PrecedenceBase") }, new[] { Nr(type: Ns + "Missing", method: "Run", assembly: TestAssembly) });

        watcher.OnAssemblyLoaded(typeof(PrecedenceDerived).Assembly);

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void Watcher_NoDerivedOtelSitesOrNoNrSites_HasNoWork()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new DistroOtelDerivedPrecedenceWatcher(new[] { Otel() }, new[] { Nr() }).HasWork, Is.False);
            Assert.That(new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel("PrecedenceBase") }, new NrSite[0]).HasWork, Is.False);
            Assert.That(new DistroOtelDerivedPrecedenceWatcher(null, null).HasWork, Is.False);
            Assert.That(new DistroOtelDerivedPrecedenceWatcher(new OtelSite[] { null }, new NrSite[] { null }).HasWork, Is.False);
            Assert.That(new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel("PrecedenceBase") }, new[] { Nr(assembly: null) }).HasWork, Is.False);
        });
    }

    [Test]
    public void Watcher_WithoutWork_IgnoresLoads()
    {
        var watcher = new DistroOtelDerivedPrecedenceWatcher(new[] { Otel() }, new[] { NrOn(typeof(PrecedenceDerived)) });

        watcher.OnAssemblyLoaded(typeof(PrecedenceDerived).Assembly);

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void Watcher_NullAssemblyOrThrowingAssembly_DoesNotThrow()
    {
        var watcher = new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel("PrecedenceBase") }, new[] { NrOn(typeof(PrecedenceDerived)) });

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => watcher.OnAssemblyLoaded(null));
            Assert.DoesNotThrow(() => watcher.OnAssemblyLoaded(new ThrowingAssembly()));
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void Watcher_OneSiteTypeThrows_OtherSitesOfTheAssemblyStillChecked()
    {
        var bad = Nr(type: "Bad.Type", method: "Run", assembly: TestAssembly);
        var watcher = new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel("PrecedenceBase") }, new[] { bad, NrOn(typeof(PrecedenceDerived)) });

        watcher.OnAssemblyLoaded(new SelectiveThrowingAssembly(typeof(PrecedenceDerived).Assembly, "Bad.Type"));

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void Watcher_ConstructorEnumerationThrows_HasNoWork()
    {
        Assert.That(new DistroOtelDerivedPrecedenceWatcher(new ThrowingEnumerable<OtelSite>(), new[] { Nr() }).HasWork, Is.False);
    }

    private sealed class ThrowingEnumerable<T> : IEnumerable<T>
    {
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("enumeration");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ThrowingAssembly : Assembly
    {
        public override AssemblyName GetName() => throw new InvalidOperationException("assembly");
    }

    // Stands in for an assembly in which one type fails to load (e.g. a missing dependency of its base type).
    private sealed class SelectiveThrowingAssembly : Assembly
    {
        private readonly Assembly _inner;
        private readonly string _throwingTypeName;

        public SelectiveThrowingAssembly(Assembly inner, string throwingTypeName)
        {
            _inner = inner;
            _throwingTypeName = throwingTypeName;
        }

        public override AssemblyName GetName() => _inner.GetName();

        // The real references, so the .NET Framework reference gate passes as it does for the inner assembly.
        public override AssemblyName[] GetReferencedAssemblies() => _inner.GetReferencedAssemblies();

        public override Type GetType(string name, bool throwOnError) =>
            name == _throwingTypeName ? throw new TypeLoadException("type") : _inner.GetType(name, throwOnError);
    }

    private sealed class ThrowingLogger : ILogger
    {
        public bool IsDebugEnabled => true;
        public bool IsErrorEnabled => true;
        public bool IsFinestEnabled => true;
        public bool IsInfoEnabled => true;
        public bool IsWarnEnabled => true;

        public void Debug(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Debug(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Error(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Error(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Finest(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Finest(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Info(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Info(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Warn(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Warn(string message, params object[] args) => throw new InvalidOperationException("logger");

        public bool IsEnabledFor(Level level) => true;
        public void Log(Level level, string message) => throw new InvalidOperationException("logger");
        public void Log(Level level, Exception ex, string message) => throw new InvalidOperationException("logger");
    }
}
