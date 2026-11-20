// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroOtelConfigDiagnosticsTests
{
    private const string Prefix = "Supportability/Dotnet/OtelDistro/Config/";

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

    private static DistroOtelConfigSnapshot Clean() => new DistroOtelConfigSnapshot
    {
        FileBasedConfigActive = false,
        EnabledTraceInstrumentationIds = new[] { "HTTPCLIENT", "AZURE" },
        Exporters = new Dictionary<string, string> { { "Traces", "none" }, { "Metrics", "none" }, { "Logs", "none" } },
    };

    private int LogCount() =>
        new[] { Level.Error, Level.Warn, Level.Info, Level.Debug, Level.Finest }.Sum(l => _logger.Messages(l).Length);

    [Test]
    public void Report_NullSnapshot_WarnsDetectionUnavailableAndCountsOnce()
    {
        DistroOtelConfigDiagnostics.Report(null, new[] { "ADONET" });

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "OTel distro: could not read OpenTelemetry's configuration (the OpenTelemetry version may differ from the one New Relic was built against); skipping the OTel configuration checks." }));
            Assert.That(_logger.Messages(Level.Error), Is.Empty);
            Assert.That(_metrics, Is.EqualTo(new[] { Prefix + "DetectionUnavailable" }));
        });
    }

    // Finding 3: a partly read snapshot (OTel drift in one place) still reports the YAML Error and what was read, plus one
    // DetectionUnavailable for the part that was not.
    [Test]
    public void Report_PartialSnapshot_ReportsYamlErrorAndDetectionUnavailableAndTheRest()
    {
        var snapshot = new DistroOtelConfigSnapshot
        {
            FileBasedConfigActive = true,
            EnabledTraceInstrumentationIds = null,
            Exporters = new Dictionary<string, string> { { "Traces", "otlp" } },
            PartialDetectionFailure = "MetricSettings: MissingMemberException: x",
        };

        DistroOtelConfigDiagnostics.Report(snapshot, new[] { "ADONET" });

        Assert.Multiple(() =>
        {
            Assert.That(_metrics, Is.EqualTo(new[] { Prefix + "FileBasedConfigActive", Prefix + "DetectionUnavailable", Prefix + "ExporterNotNone/Traces" }));
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(2));
            Assert.That(_logger.Messages(Level.Error)[0], Does.StartWith("OTel distro: OpenTelemetry file-based (YAML) configuration is active"));
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "OTel distro: could not read part of OpenTelemetry's configuration (MetricSettings: MissingMemberException: x); the OpenTelemetry version may differ from the one New Relic was built against. The OTel configuration checks that depend on it are skipped." }));
        });
    }

    [Test]
    public void Report_CleanSnapshot_ReportsNothing()
    {
        DistroOtelConfigDiagnostics.Report(Clean(), new[] { "ADONET", "SQLCLIENT" });

        Assert.Multiple(() =>
        {
            Assert.That(LogCount(), Is.Zero);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void Report_FileBasedConfigActive_LogsOneErrorListingWhatNewRelicCannotEnforce()
    {
        var snapshot = Clean();
        snapshot.FileBasedConfigActive = true;

        DistroOtelConfigDiagnostics.Report(snapshot, new[] { "ADONET" });

        var errors = _logger.Messages(Level.Error);
        Assert.Multiple(() =>
        {
            Assert.That(errors, Has.Length.EqualTo(1));
            Assert.That(errors[0], Does.Contain("file-based (YAML) configuration is active"));
            Assert.That(errors[0], Does.Contain("OTel exporters"));
            Assert.That(errors[0], Does.Contain("OTel instrumentation IDs"));
            Assert.That(errors[0], Does.Contain("OTel propagators"));
            Assert.That(errors[0], Does.Contain("OTel log processors"));
            Assert.That(errors[0], Does.Contain("OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED=false"));
            Assert.That(_logger.Messages(Level.Warn), Is.Empty);
            Assert.That(_metrics, Is.EqualTo(new[] { Prefix + "FileBasedConfigActive" }));
        });
    }

    [Test]
    public void Report_DisableIdEnabled_WarnsAndCountsPerId()
    {
        var snapshot = Clean();
        snapshot.EnabledTraceInstrumentationIds = new[] { "HTTPCLIENT", "ADONET", "SQLCLIENT" };

        DistroOtelConfigDiagnostics.Report(snapshot, new[] { "ADONET", "SQLCLIENT", "MYSQLDATA" });

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Has.Length.EqualTo(2));
            Assert.That(_logger.Messages(Level.Warn)[0], Is.EqualTo("OTel distro: OTel trace instrumentation ADONET is enabled although New Relic turns it off so New Relic instruments the same library; OTel may take precedence over New Relic there (duplicate or missing telemetry)."));
            Assert.That(_logger.Messages(Level.Warn)[1], Does.Contain("SQLCLIENT"));
            Assert.That(_metrics, Is.EqualTo(new[] { Prefix + "OtelInstrumentationReenabled/ADONET", Prefix + "OtelInstrumentationReenabled/SQLCLIENT" }));
        });
    }

    [Test]
    public void Report_EnabledIdNotInDisableList_IsIgnored()
    {
        // HTTPCLIENT and AZURE are enabled in Clean() but are not in the deny list (kept on by design).
        DistroOtelConfigDiagnostics.Report(Clean(), new[] { "ADONET" });

        Assert.That(_metrics, Is.Empty);
    }

    [Test]
    public void Report_IdMatchIsCaseInsensitive_AndDuplicateDisableIdReportsOnce()
    {
        var snapshot = Clean();
        snapshot.EnabledTraceInstrumentationIds = new[] { "adonet" };

        DistroOtelConfigDiagnostics.Report(snapshot, new[] { "ADONET", "ADONET", "", null, "  " });

        Assert.That(_metrics, Is.EqualTo(new[] { Prefix + "OtelInstrumentationReenabled/ADONET" }));
    }

    [TestCase("Traces")]
    [TestCase("Metrics")]
    [TestCase("Logs")]
    public void Report_ExporterNotNone_ErrorsAndCountsPerSignal(string signal)
    {
        var snapshot = Clean();
        var exporters = snapshot.Exporters.ToDictionary(e => e.Key, e => e.Value);
        exporters[signal] = "otlp";
        snapshot.Exporters = exporters;

        DistroOtelConfigDiagnostics.Report(snapshot, new[] { "ADONET" });

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Error), Is.EqualTo(new[] { $"OTel distro: the OTel {signal} exporter is 'otlp', not 'none'; OpenTelemetry may send {signal} data to its own backend alongside New Relic." }));
            Assert.That(_metrics, Is.EqualTo(new[] { Prefix + "ExporterNotNone/" + signal }));
        });
    }

    [TestCase("NONE")]
    [TestCase(" none ")]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase(null)]
    public void Report_ExporterNoneOrBlank_ReportsNothing(string value)
    {
        var snapshot = Clean();
        snapshot.Exporters = new Dictionary<string, string> { { "Traces", value } };

        DistroOtelConfigDiagnostics.Report(snapshot, new string[0]);

        Assert.Multiple(() =>
        {
            Assert.That(LogCount(), Is.Zero);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void Report_EverythingWrong_ReportsEachFinding()
    {
        var snapshot = new DistroOtelConfigSnapshot
        {
            FileBasedConfigActive = true,
            EnabledTraceInstrumentationIds = new[] { "ADONET" },
            Exporters = new Dictionary<string, string> { { "Traces", "otlp" }, { "Logs", "console" } },
        };

        DistroOtelConfigDiagnostics.Report(snapshot, new[] { "ADONET" });

        Assert.That(_metrics, Is.EquivalentTo(new[]
        {
            Prefix + "FileBasedConfigActive",
            Prefix + "OtelInstrumentationReenabled/ADONET",
            Prefix + "ExporterNotNone/Traces",
            Prefix + "ExporterNotNone/Logs",
        }));
    }

    [Test]
    public void Report_NullMembersAndNullDisableIds_ReportNothingAndDoNotThrow()
    {
        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => DistroOtelConfigDiagnostics.Report(new DistroOtelConfigSnapshot(), new[] { "ADONET" }));
            Assert.DoesNotThrow(() => DistroOtelConfigDiagnostics.Report(Clean(), null));
            Assert.That(LogCount(), Is.Zero);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [Test]
    public void Report_MetricsRecordedBeforeSinkIsSet_AreQueuedAndFlushed()
    {
        DistroConfigMetrics.ResetForTesting();
        var snapshot = Clean();
        snapshot.FileBasedConfigActive = true;

        DistroOtelConfigDiagnostics.Report(snapshot, new[] { "ADONET" });
        Assert.That(_metrics, Is.Empty);

        DistroConfigMetrics.SetMetricSink(_metrics.Add);

        Assert.That(_metrics, Is.EqualTo(new[] { Prefix + "FileBasedConfigActive" }));
    }

    [Test]
    public void Report_LoggerThrows_DoesNotThrowAndStillRecordsEveryMetric()
    {
        Log.Initialize(new ThrowingLogger());
        var snapshot = Clean();
        snapshot.FileBasedConfigActive = true;
        snapshot.EnabledTraceInstrumentationIds = new[] { "ADONET", "SQLCLIENT" };
        snapshot.Exporters = new Dictionary<string, string> { { "Traces", "otlp" }, { "Metrics", "none" }, { "Logs", "console" } };

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => DistroOtelConfigDiagnostics.Report(snapshot, new[] { "ADONET", "SQLCLIENT" }));
            Assert.DoesNotThrow(() => DistroOtelConfigDiagnostics.Report(null, null));
            Assert.That(_metrics, Is.EqualTo(new[]
            {
                Prefix + "FileBasedConfigActive",
                Prefix + "OtelInstrumentationReenabled/ADONET",
                Prefix + "OtelInstrumentationReenabled/SQLCLIENT",
                Prefix + "ExporterNotNone/Traces",
                Prefix + "ExporterNotNone/Logs",
                Prefix + "DetectionUnavailable",
            }));
        });
    }

    [Test]
    public void Report_SnapshotCollectionThrows_DoesNotThrowAndKeepsEarlierMetrics()
    {
        var snapshot = Clean();
        snapshot.FileBasedConfigActive = true;
        snapshot.EnabledTraceInstrumentationIds = new ThrowingCollection();

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => DistroOtelConfigDiagnostics.Report(snapshot, new[] { "ADONET" }));
            Assert.That(_metrics, Is.EqualTo(new[] { Prefix + "FileBasedConfigActive" }));
        });
    }

    private sealed class ThrowingCollection : IReadOnlyCollection<string>
    {
        public int Count => throw new InvalidOperationException("collection");

        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("collection");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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
