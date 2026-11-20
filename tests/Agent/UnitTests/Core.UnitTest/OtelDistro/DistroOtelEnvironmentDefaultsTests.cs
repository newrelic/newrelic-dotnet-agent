// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroOtelEnvironmentDefaultsTests
{
    private const string TracesExporter = "OTEL_TRACES_EXPORTER";
    private const string MetricsExporter = "OTEL_METRICS_EXPORTER";
    private const string LogsExporter = "OTEL_LOGS_EXPORTER";
    private const string Propagators = "OTEL_PROPAGATORS";
    private const string GlobalTracesSwitch = "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED";
    private const string YamlSwitch = "OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED";

    private DistroRecordingLogger _logger;
    private List<string> _metrics;

    [SetUp]
    public void SetUp()
    {
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
    }

    private static string IdVar(string id) => $"OTEL_DOTNET_AUTO_TRACES_{id}_INSTRUMENTATION_ENABLED";

    [Test]
    public void Apply_UnsetId_SetsFalseAndLogsInfo()
    {
        var env = new FakeEnvironment();

        var result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET", "SQLCLIENT" }, false);

        Assert.Multiple(() =>
        {
            Assert.That(env.Values[IdVar("ADONET")], Is.EqualTo("false"));
            Assert.That(env.Values[IdVar("SQLCLIENT")], Is.EqualTo("false"));
            Assert.That(result.AppliedDisables, Is.EqualTo(new[] { "ADONET", "SQLCLIENT" }));
            Assert.That(result.CustomerValuesKept, Is.Empty);
            Assert.That(_logger.Messages(Level.Info), Does.Contain("OTel distro: set OTEL_DOTNET_AUTO_TRACES_ADONET_INSTRUMENTATION_ENABLED=false so New Relic instruments this library (the customer had not set it)."));
            Assert.That(_logger.Messages(Level.Info).Count(m => m.Contains("so New Relic instruments this library")), Is.EqualTo(2));
        });
    }

    [TestCase("FALSE")]
    [TestCase("true")]
    [TestCase(" True ")]
    public void Apply_IdPresent_KeepsCustomerValueAndLogsInfo(string customerValue)
    {
        var env = new FakeEnvironment();
        env.Values[IdVar("ADONET")] = customerValue;

        var result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET" }, false);

        Assert.Multiple(() =>
        {
            Assert.That(env.Values[IdVar("ADONET")], Is.EqualTo(customerValue));
            Assert.That(env.Writes.Select(w => w.Key), Does.Not.Contain(IdVar("ADONET")));
            Assert.That(result.AppliedDisables, Is.Empty);
            Assert.That(result.CustomerValuesKept, Is.EqualTo(new[] { new KeyValuePair<string, string>("ADONET", customerValue) }));
            Assert.That(_logger.Messages(Level.Info), Does.Contain($"OTel distro: kept the customer's OTEL_DOTNET_AUTO_TRACES_ADONET_INSTRUMENTATION_ENABLED='{customerValue}'; New Relic does not override it."));
        });
    }

    [Test]
    public void Apply_NullOrBlankIds_AreSkipped()
    {
        var env = new FakeEnvironment();

        var result = DistroOtelEnvironmentDefaults.Apply(env, new[] { null, "", " ", "NPGSQL" }, false);

        Assert.Multiple(() =>
        {
            Assert.That(result.AppliedDisables, Is.EqualTo(new[] { "NPGSQL" }));
            Assert.That(env.Writes.Select(w => w.Key).Where(k => k.StartsWith("OTEL_DOTNET_AUTO_", StringComparison.Ordinal)), Is.EqualTo(new[] { IdVar("NPGSQL") }));
        });
    }

    [Test]
    public void Apply_NullDisableIds_StillForcesExporters()
    {
        var env = new FakeEnvironment();

        var result = DistroOtelEnvironmentDefaults.Apply(env, null, false);

        Assert.Multiple(() =>
        {
            Assert.That(result.AppliedDisables, Is.Empty);
            Assert.That(env.Values[TracesExporter], Is.EqualTo("none"));
            Assert.That(_logger.Messages(Level.Error), Is.Empty);
        });
    }

    [Test]
    public void Apply_ExportersUnset_SetsNoneWithoutWarning()
    {
        var env = new FakeEnvironment();

        var result = DistroOtelEnvironmentDefaults.Apply(env, new string[0], false);

        Assert.Multiple(() =>
        {
            Assert.That(env.Values[TracesExporter], Is.EqualTo("none"));
            Assert.That(env.Values[MetricsExporter], Is.EqualTo("none"));
            Assert.That(env.Values[LogsExporter], Is.EqualTo("none"));
            Assert.That(result.ExportersOverridden, Is.Empty);
            Assert.That(_logger.Messages(Level.Warn), Is.Empty);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [TestCase("none")]
    [TestCase(" NONE ")]
    [TestCase("")]
    public void Apply_ExporterAlreadyNoneOrEmpty_SetsNoneWithoutWarning(string prior)
    {
        var env = new FakeEnvironment();
        env.Values[TracesExporter] = prior;

        var result = DistroOtelEnvironmentDefaults.Apply(env, new string[0], false);

        Assert.Multiple(() =>
        {
            Assert.That(env.Values[TracesExporter], Is.EqualTo("none"));
            Assert.That(result.ExportersOverridden, Is.Empty);
            Assert.That(_logger.Messages(Level.Warn), Is.Empty);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void Apply_ExportersOtlp_ForcedToNoneWithWarningAndMetricPerSignal()
    {
        var env = new FakeEnvironment();
        env.Values[TracesExporter] = "otlp";
        env.Values[MetricsExporter] = "otlp,console";
        env.Values[LogsExporter] = "otlp";

        var result = DistroOtelEnvironmentDefaults.Apply(env, new string[0], false);

        Assert.Multiple(() =>
        {
            Assert.That(env.Values[TracesExporter], Is.EqualTo("none"));
            Assert.That(env.Values[MetricsExporter], Is.EqualTo("none"));
            Assert.That(env.Values[LogsExporter], Is.EqualTo("none"));
            Assert.That(result.ExportersOverridden, Is.EqualTo(new[]
            {
                new KeyValuePair<string, string>("Traces", "otlp"),
                new KeyValuePair<string, string>("Metrics", "otlp,console"),
                new KeyValuePair<string, string>("Logs", "otlp"),
            }));
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[]
            {
                "OTel distro: OTEL_TRACES_EXPORTER was 'otlp'; New Relic set it to 'none' (New Relic exports this signal).",
                "OTel distro: OTEL_METRICS_EXPORTER was 'otlp,console'; New Relic set it to 'none' (New Relic exports this signal).",
                "OTel distro: OTEL_LOGS_EXPORTER was 'otlp'; New Relic set it to 'none' (New Relic exports this signal).",
            }));
            Assert.That(_metrics, Is.EqualTo(new[]
            {
                "Supportability/Dotnet/OtelDistro/Config/ExporterNotNone/Traces",
                "Supportability/Dotnet/OtelDistro/Config/ExporterNotNone/Metrics",
                "Supportability/Dotnet/OtelDistro/Config/ExporterNotNone/Logs",
            }));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("tracecontext,baggage")]
    [TestCase(" TraceContext , Baggage ")]
    public void Apply_PropagatorsUnsetOrDefault_NothingReported(string value)
    {
        var env = new FakeEnvironment();
        if (value != null)
        {
            env.Values[Propagators] = value;
        }

        var result = DistroOtelEnvironmentDefaults.Apply(env, new string[0], false);

        Assert.Multiple(() =>
        {
            Assert.That(result.NonDefaultPropagators, Is.Null);
            Assert.That(env.Writes.Select(w => w.Key), Does.Not.Contain(Propagators));
            Assert.That(_logger.Messages(Level.Warn), Is.Empty);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void Apply_PropagatorsB3_WarnsAndRecordsMetric_ValueUnchanged()
    {
        var env = new FakeEnvironment();
        env.Values[Propagators] = "b3";

        var result = DistroOtelEnvironmentDefaults.Apply(env, new string[0], false);

        Assert.Multiple(() =>
        {
            Assert.That(result.NonDefaultPropagators, Is.EqualTo("b3"));
            Assert.That(env.Values[Propagators], Is.EqualTo("b3"));
            Assert.That(env.Writes.Select(w => w.Key), Does.Not.Contain(Propagators));
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[]
            {
                "OTel distro: OTEL_PROPAGATORS is 'b3' (not the default tracecontext,baggage); kept as set. New Relic propagates through its own instrumentation; OTel's propagators apply only where OTel instruments.",
            }));
            Assert.That(_metrics, Is.EqualTo(new[] { "Supportability/Dotnet/OtelDistro/Config/PropagatorsNotDefault" }));
        });
    }

    [Test]
    public void Apply_NeverWritesGlobalTracesSwitch_NorTouchesLogsOrMetricsIds()
    {
        var env = new FakeEnvironment();

        DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET", "MYSQLDATA" }, false);

        Assert.Multiple(() =>
        {
            Assert.That(env.Writes.Select(w => w.Key), Does.Not.Contain(GlobalTracesSwitch));
            Assert.That(env.Writes.Select(w => w.Key).Where(k => k.StartsWith("OTEL_DOTNET_AUTO_LOGS_", StringComparison.Ordinal) || k.StartsWith("OTEL_DOTNET_AUTO_METRICS_", StringComparison.Ordinal)), Is.Empty);
            Assert.That(env.Writes.Select(w => w.Key), Is.EquivalentTo(new[] { TracesExporter, MetricsExporter, LogsExporter, IdVar("ADONET"), IdVar("MYSQLDATA") }));
        });
    }

    [Test]
    public void Apply_SetThrows_DoesNotThrowAndLogsOneError()
    {
        var env = new FakeEnvironment { ThrowOnSet = true };
        env.Values[TracesExporter] = "otlp";

        DistroOtelDefaultsResult result = null;
        Assert.DoesNotThrow(() => result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET", "SQLCLIENT" }, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.AppliedDisables, Is.Empty);
            Assert.That(result.ExportersOverridden, Is.Empty);
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Error)[0], Does.StartWith("OTel distro: could not apply New Relic's OTel environment defaults"));
        });
    }

    [Test]
    public void Apply_GetThrows_DoesNotThrowAndLogsOneError()
    {
        var env = new FakeEnvironment { ThrowOnGet = true };

        DistroOtelDefaultsResult result = null;
        Assert.DoesNotThrow(() => result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET" }, false));

        Assert.Multiple(() =>
        {
            Assert.That(result.NonDefaultPropagators, Is.Null);
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void Apply_NullEnvironment_DoesNotThrowAndLogsOneError()
    {
        DistroOtelDefaultsResult result = null;
        Assert.DoesNotThrow(() => result = DistroOtelEnvironmentDefaults.Apply(null, new[] { "ADONET" }, false));

        Assert.Multiple(() =>
        {
            Assert.That(result.AppliedDisables, Is.Empty);
            Assert.That(result.CustomerValuesKept, Is.Empty);
            Assert.That(result.ExportersOverridden, Is.Empty);
            Assert.That(result.NonDefaultPropagators, Is.Null);
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void Apply_MetricsBeforeSink_AreQueuedThenFlushed()
    {
        DistroConfigMetrics.ResetForTesting();
        var env = new FakeEnvironment();
        env.Values[TracesExporter] = "otlp";
        env.Values[Propagators] = "b3";

        DistroOtelEnvironmentDefaults.Apply(env, new string[0], false);

        var flushed = new List<string>();
        DistroConfigMetrics.SetMetricSink(flushed.Add);

        Assert.That(flushed, Is.EqualTo(new[]
        {
            "Supportability/Dotnet/OtelDistro/Config/ExporterNotNone/Traces",
            "Supportability/Dotnet/OtelDistro/Config/PropagatorsNotDefault",
        }));
    }

    [Test]
    public void Apply_AllStepsSucceed_FailureMessageIsNull()
    {
        var result = DistroOtelEnvironmentDefaults.Apply(new FakeEnvironment(), new[] { "ADONET" }, false);

        Assert.That(result.FailureMessage, Is.Null);
    }

    // Finding 6: OTel reads the per-ID switch with bool.Parse/TryParse; anything else is "unset" (instrumentation on) or,
    // under fail-fast, a start-up failure. The value is still the customer's and is kept.
    [TestCase("")]
    [TestCase("0")]
    [TestCase("1")]
    [TestCase("no")]
    public void Apply_IdPresentButNotABoolean_KeepsItAndWarnsThatOtelInstrumentationStaysOn(string customerValue)
    {
        var env = new FakeEnvironment();
        env.Values[IdVar("ADONET")] = customerValue;

        var result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET" }, false);

        Assert.Multiple(() =>
        {
            Assert.That(env.Values[IdVar("ADONET")], Is.EqualTo(customerValue));
            Assert.That(env.Writes.Select(w => w.Key), Does.Not.Contain(IdVar("ADONET")));
            Assert.That(result.CustomerValuesKept, Is.EqualTo(new[] { new KeyValuePair<string, string>("ADONET", customerValue) }));
            Assert.That(_logger.Messages(Level.Info).Where(m => m.Contains("ADONET")), Is.Empty);
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[]
            {
                $"OTel distro: kept the customer's OTEL_DOTNET_AUTO_TRACES_ADONET_INSTRUMENTATION_ENABLED='{customerValue}'; New Relic does not override it, but OpenTelemetry does not read '{customerValue}' as true or false: it treats the setting as unset, so this OTel instrumentation stays on (unless OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED=false), or, with OTEL_DOTNET_AUTO_FAIL_FAST=true, OpenTelemetry fails to start. Set it to true or false."
            }));
        });
    }

    // Finding 2: YAML mode is OTel's exact, case-sensitive, untrimmed "true".
    [Test]
    public void Apply_YamlMode_WritesEverythingButLogsOneWarningInsteadOfThePerVariableLines()
    {
        var env = new FakeEnvironment();
        env.Values[YamlSwitch] = "true";
        env.Values[TracesExporter] = "otlp";
        env.Values[Propagators] = "b3";
        env.Values[IdVar("SQLCLIENT")] = "true";
        env.Values[IdVar("NPGSQL")] = "nonsense";

        var result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET", "MYSQLDATA", "SQLCLIENT", "NPGSQL" }, false);

        Assert.Multiple(() =>
        {
            Assert.That(env.Values[TracesExporter], Is.EqualTo("none"));
            Assert.That(env.Values[MetricsExporter], Is.EqualTo("none"));
            Assert.That(env.Values[LogsExporter], Is.EqualTo("none"));
            Assert.That(env.Values[IdVar("ADONET")], Is.EqualTo("false"));
            Assert.That(env.Values[IdVar("MYSQLDATA")], Is.EqualTo("false"));
            Assert.That(env.Values[IdVar("SQLCLIENT")], Is.EqualTo("true"));
            Assert.That(env.Values[IdVar("NPGSQL")], Is.EqualTo("nonsense"));
            Assert.That(env.Values[Propagators], Is.EqualTo("b3"));
            Assert.That(result.FileBasedConfigActive, Is.True);
            Assert.That(result.AppliedDisables, Is.EqualTo(new[] { "ADONET", "MYSQLDATA" }));
            Assert.That(result.CustomerValuesKept, Has.Count.EqualTo(2));
            Assert.That(result.ExportersOverridden, Is.EqualTo(new[] { new KeyValuePair<string, string>("Traces", "otlp") }));
            Assert.That(result.NonDefaultPropagators, Is.EqualTo("b3"));
            Assert.That(result.FailureMessage, Is.Null);
            Assert.That(_logger.Messages(Level.Info), Is.Empty);
            Assert.That(_logger.Messages(Level.Error), Is.Empty);
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[]
            {
                "OTel distro: OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED=true, so OpenTelemetry ignores its OTEL_* environment variables; New Relic's OTel environment defaults (exporters none; OTel trace instrumentation off: ADONET, MYSQLDATA) were written but have no effect. See the file-based configuration error reported after OpenTelemetry start-up."
            }));
            Assert.That(_metrics, Is.Empty, "OTel ignores the env exporter/propagator values in YAML mode; the YAML-effective ones are reported after start-up");
        });
    }

    [Test]
    public void Apply_YamlModeNothingDisabled_SaysNone()
    {
        var env = new FakeEnvironment();
        env.Values[YamlSwitch] = "true";

        DistroOtelEnvironmentDefaults.Apply(env, new string[0], false);

        Assert.That(_logger.Messages(Level.Warn).Single(), Does.Contain("OTel trace instrumentation off: none)"));
    }

    [TestCase("True")]
    [TestCase(" true")]
    [TestCase("true ")]
    [TestCase("1")]
    [TestCase("false")]
    [TestCase("")]
    [TestCase(null)]
    public void Apply_NotExactlyTrue_IsTheNormalEnvMode(string yamlValue)
    {
        var env = new FakeEnvironment();
        if (yamlValue != null)
        {
            env.Values[YamlSwitch] = yamlValue;
        }

        env.Values[TracesExporter] = "otlp";

        var result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET" }, false);

        Assert.Multiple(() =>
        {
            Assert.That(result.FileBasedConfigActive, Is.False);
            Assert.That(_logger.Messages(Level.Info), Does.Contain("OTel distro: set OTEL_DOTNET_AUTO_TRACES_ADONET_INSTRUMENTATION_ENABLED=false so New Relic instruments this library (the customer had not set it)."));
            Assert.That(_logger.Messages(Level.Warn).Any(m => m.Contains("ignores its OTEL_* environment variables")), Is.False);
            Assert.That(_metrics, Is.EqualTo(new[] { "Supportability/Dotnet/OtelDistro/Config/ExporterNotNone/Traces" }));
        });
    }

    [Test]
    public void Apply_ReadingTheYamlSwitchThrows_NormalModeAndOneFailureRecorded()
    {
        var env = new FakeEnvironment { ThrowOnGetFor = YamlSwitch };

        var result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET" }, false);

        Assert.Multiple(() =>
        {
            Assert.That(result.FileBasedConfigActive, Is.False);
            Assert.That(result.FailureMessage, Is.EqualTo("get failed"));
            Assert.That(env.Values[TracesExporter], Is.EqualTo("none"));
            Assert.That(env.Values[IdVar("ADONET")], Is.EqualTo("false"));
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void FormatSummary_YamlMode_SaysOtelIgnoresTheDefaults()
    {
        var env = new FakeEnvironment();
        env.Values[YamlSwitch] = "true";
        var result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET" }, false);

        Assert.That(DistroOtelEnvironmentDefaults.FormatSummary(result), Is.EqualTo(
            "OTel environment defaults: YAML mode: OTel ignores these env defaults; exporters set to none (none overridden); OTel trace instrumentation disabled: ADONET; customer values kept: none; OTEL_PROPAGATORS: default."));
    }

    [Test]
    public void FormatSummary_YamlModeAndFailure()
    {
        var result = new DistroOtelDefaultsResult(new string[0], new KeyValuePair<string, string>[0], new KeyValuePair<string, string>[0], null, "set failed", true);

        Assert.That(DistroOtelEnvironmentDefaults.FormatSummary(result), Does.StartWith(
            "OTel environment defaults: YAML mode: OTel ignores these env defaults; NOT ALL APPLIED (FAILED: set failed);"));
    }

    [Test]
    public void Apply_SetThrows_KeepsFirstFailureMessage()
    {
        var result = DistroOtelEnvironmentDefaults.Apply(new FakeEnvironment { ThrowOnSet = true }, new[] { "ADONET" }, false);

        Assert.That(result.FailureMessage, Is.EqualTo("set failed"));
    }

    [Test]
    public void Apply_NullEnvironment_ReportsFailureMessage()
    {
        var result = DistroOtelEnvironmentDefaults.Apply(null, new[] { "ADONET" }, false);

        Assert.That(result.FailureMessage, Is.EqualTo("no environment to apply them to"));
    }

    // Core start failed: Core's logger may throw. The environment writes must still all happen.
    [Test]
    public void Apply_LoggerThrows_StillWritesEverythingAndDoesNotThrow()
    {
        Log.Initialize(new ThrowingLogger());
        var env = new FakeEnvironment();
        env.Values[TracesExporter] = "otlp";
        env.Values[Propagators] = "b3";
        env.Values[IdVar("SQLCLIENT")] = "true";

        DistroOtelDefaultsResult result = null;
        Assert.DoesNotThrow(() => result = DistroOtelEnvironmentDefaults.Apply(env, new[] { "ADONET", "SQLCLIENT" }, false));

        Assert.Multiple(() =>
        {
            Assert.That(env.Values[TracesExporter], Is.EqualTo("none"));
            Assert.That(env.Values[MetricsExporter], Is.EqualTo("none"));
            Assert.That(env.Values[LogsExporter], Is.EqualTo("none"));
            Assert.That(env.Values[IdVar("ADONET")], Is.EqualTo("false"));
            Assert.That(env.Values[IdVar("SQLCLIENT")], Is.EqualTo("true"));
            Assert.That(result.ExportersOverridden, Has.Count.EqualTo(1));
            Assert.That(result.NonDefaultPropagators, Is.EqualTo("b3"));
            Assert.That(result.FailureMessage, Is.Null);
            Assert.That(_metrics, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Apply_LoggerAndSetThrow_DoesNotThrow()
    {
        Log.Initialize(new ThrowingLogger());

        DistroOtelDefaultsResult result = null;
        Assert.DoesNotThrow(() => result = DistroOtelEnvironmentDefaults.Apply(new FakeEnvironment { ThrowOnSet = true }, new[] { "ADONET" }, false));

        Assert.That(result.FailureMessage, Is.EqualTo("set failed"));
    }

    [Test]
    public void FormatSummary_NullResult_FixedLine()
    {
        Assert.That(DistroOtelEnvironmentDefaults.FormatSummary(null), Is.EqualTo("OTel environment defaults: no result."));
    }

    [Test]
    public void FormatSummary_NothingNotable()
    {
        var result = DistroOtelEnvironmentDefaults.Apply(new FakeEnvironment(), new string[0], false);

        Assert.That(DistroOtelEnvironmentDefaults.FormatSummary(result), Is.EqualTo(
            "OTel environment defaults: exporters set to none (none overridden); OTel trace instrumentation disabled: none; customer values kept: none; OTEL_PROPAGATORS: default."));
    }

    [Test]
    public void FormatSummary_EverythingNotable()
    {
        var result = new DistroOtelDefaultsResult(
            new[] { "ADONET", "NPGSQL" },
            new[] { new KeyValuePair<string, string>("SQLCLIENT", "true") },
            new[] { new KeyValuePair<string, string>("Traces", "otlp"), new KeyValuePair<string, string>("Logs", "console") },
            "b3",
            "set failed",
            false);

        Assert.That(DistroOtelEnvironmentDefaults.FormatSummary(result), Is.EqualTo(
            "OTel environment defaults: NOT ALL APPLIED (FAILED: set failed); exporters may not be set to none (overridden: Traces='otlp', Logs='console'); OTel trace instrumentation disabled: ADONET, NPGSQL; customer values kept: SQLCLIENT='true'; OTEL_PROPAGATORS: 'b3' (not default, kept)."));
    }

    [Test]
    public void FormatSummary_Failure_DoesNotClaimExportersWereSetToNone()
    {
        var result = DistroOtelEnvironmentDefaults.Apply(new FakeEnvironment { ThrowOnSet = true }, new[] { "ADONET" }, false);

        var summary = DistroOtelEnvironmentDefaults.FormatSummary(result);

        Assert.Multiple(() =>
        {
            Assert.That(summary, Does.Not.Contain("exporters set to none"));
            Assert.That(summary, Does.StartWith("OTel environment defaults: NOT ALL APPLIED (FAILED: set failed); exporters may not be set to none (none overridden); OTel trace instrumentation disabled: none;"));
        });
    }

    // ---- NLog bridge (OTEL_DOTNET_AUTO_LOGS_ENABLE_NLOG_BRIDGE): OTel's bridge is opt-in and off by default. New
    //      Relic never writes it (a write would leak into child processes as a customer value); it only warns when NR's
    //      NLog instrumentation is compiled and the customer turned the bridge on (the per-call dedupe cannot catch it). ----

    private const string NLogBridgeVar = "OTEL_DOTNET_AUTO_LOGS_ENABLE_NLOG_BRIDGE";

    [TestCase(true)]
    [TestCase(false)]
    public void Apply_NeverWritesTheNLogBridgeVariable(bool nrNLogCompiled)
    {
        var env = new FakeEnvironment();

        DistroOtelEnvironmentDefaults.Apply(env, new string[0], nrNLogCompiled);

        Assert.That(env.Writes.Select(w => w.Key), Does.Not.Contain(NLogBridgeVar));
    }

    [TestCase("true")]
    [TestCase("TRUE")]
    public void Apply_NrNLogCompiledAndCustomerBridgeOn_WarnsDuplicateAndCounts(string customerValue)
    {
        var env = new FakeEnvironment();
        env.Values[NLogBridgeVar] = customerValue;

        DistroOtelEnvironmentDefaults.Apply(env, new string[0], nrNLogInstrumentationCompiled: true);

        Assert.Multiple(() =>
        {
            Assert.That(env.Writes.Select(w => w.Key), Does.Not.Contain(NLogBridgeVar));
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[]
            {
                $"OTel distro: OTEL_DOTNET_AUTO_LOGS_ENABLE_NLOG_BRIDGE='{customerValue}' turns OpenTelemetry's NLog bridge on while New Relic's NLog instrumentation also forwards NLog logs, so each NLog line may be forwarded twice (duplicate logs). New Relic does not change the setting; set it to false to forward each NLog line once."
            }));
            Assert.That(_metrics, Is.EqualTo(new[] { DistroOtelEnvironmentDefaults.NLogBridgeKeptOnMetricName }));
        });
    }

    [TestCase("false")]
    [TestCase("nonsense")]
    [TestCase(null)]
    public void Apply_NrNLogCompiledAndCustomerBridgeNotOn_NoWarnNoMetric(string customerValue)
    {
        var env = new FakeEnvironment();
        if (customerValue != null)
        {
            env.Values[NLogBridgeVar] = customerValue;
        }

        DistroOtelEnvironmentDefaults.Apply(env, new string[0], nrNLogInstrumentationCompiled: true);

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.Empty);
            Assert.That(_metrics, Does.Not.Contain(DistroOtelEnvironmentDefaults.NLogBridgeKeptOnMetricName));
        });
    }

    [Test]
    public void Apply_NrNLogNotCompiled_IgnoresCustomerBridgeOn()
    {
        var env = new FakeEnvironment();
        env.Values[NLogBridgeVar] = "true";

        DistroOtelEnvironmentDefaults.Apply(env, new string[0], nrNLogInstrumentationCompiled: false);

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.Empty);
            Assert.That(_metrics, Does.Not.Contain(DistroOtelEnvironmentDefaults.NLogBridgeKeptOnMetricName));
        });
    }

    [Test]
    public void Apply_YamlMode_SkipsTheNLogBridgeWarning()
    {
        var env = new FakeEnvironment();
        env.Values[YamlSwitch] = "true";
        env.Values[NLogBridgeVar] = "true";

        DistroOtelEnvironmentDefaults.Apply(env, new string[0], nrNLogInstrumentationCompiled: true);

        Assert.Multiple(() =>
        {
            // In YAML mode OTel ignores the env var, so the duplicate-logs premise does not hold; only the YAML warning fires.
            Assert.That(_metrics, Does.Not.Contain(DistroOtelEnvironmentDefaults.NLogBridgeKeptOnMetricName));
            Assert.That(_logger.Messages(Level.Warn), Has.None.Contains("NLog bridge"));
        });
    }

    [Test]
    public void FormatSummary_NullLists_DoesNotThrow()
    {
        // The result's constructor null-coalesces its list parameters so FormatSummary (documented "never throws")
        // cannot NRE on a result built with null lists.
        var result = new DistroOtelDefaultsResult(null, null, null, null, null, false);

        string summary = null;
        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => summary = DistroOtelEnvironmentDefaults.FormatSummary(result));
            Assert.That(summary, Is.EqualTo(
                "OTel environment defaults: exporters set to none (none overridden); OTel trace instrumentation disabled: none; customer values kept: none; OTEL_PROPAGATORS: default."));
            Assert.That(result.AppliedDisables, Is.Empty);
            Assert.That(result.CustomerValuesKept, Is.Empty);
            Assert.That(result.ExportersOverridden, Is.Empty);
        });
    }

    private sealed class ThrowingLogger : ILogger
    {
        public bool IsDebugEnabled => true;
        public bool IsErrorEnabled => true;
        public bool IsFinestEnabled => true;
        public bool IsInfoEnabled => true;
        public bool IsWarnEnabled => true;

        public void Debug(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Debug(string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Error(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Error(string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Finest(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Finest(string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Info(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Info(string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Warn(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Warn(string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public bool IsEnabledFor(Level level) => true;
        public void Log(Level level, string message) => throw new InvalidOperationException("logger closed");
        public void Log(Level level, Exception ex, string message) => throw new InvalidOperationException("logger closed");
    }

    private sealed class FakeEnvironment : IDistroEnvironmentVariables
    {
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();
        public List<KeyValuePair<string, string>> Writes { get; } = new List<KeyValuePair<string, string>>();
        public bool ThrowOnSet { get; set; }
        public bool ThrowOnGet { get; set; }
        public string ThrowOnGetFor { get; set; }

        public string Get(string name)
        {
            if (ThrowOnGet || name == ThrowOnGetFor)
            {
                throw new InvalidOperationException("get failed");
            }

            return Values.TryGetValue(name, out var value) ? value : null;
        }

        public void Set(string name, string value)
        {
            if (ThrowOnSet)
            {
                throw new InvalidOperationException("set failed");
            }

            Writes.Add(new KeyValuePair<string, string>(name, value));
            Values[name] = value;
        }
    }
}
