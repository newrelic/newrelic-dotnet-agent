// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroMethodResolutionDiagnosticsTests
{
    private const string AmbiguousText = "OTel distro: cannot tell overloads of NS.Worker.DoWork with 2 parameters apart and they differ in async-ness; treating the call as not async (deliberate fallback).";
    private const string NotFoundText = "OTel distro: could not identify the method NS.Worker.DoWork with 2 parameters that fired; treating the call as not async.";

    private const string AmbiguousMetric = "Supportability/Dotnet/OtelDistro/MethodResolution/Ambiguous";
    private const string NotFoundMetric = "Supportability/Dotnet/OtelDistro/MethodResolution/NotFound";

    private DistroRecordingLogger _logger;

    [SetUp]
    public void SetUp()
    {
        DistroMethodResolutionDiagnostics.ResetForTesting();
        DistroConfigMetrics.ResetForTesting();
        _logger = new DistroRecordingLogger();
        Log.Initialize(_logger);
    }

    [TearDown]
    public void TearDown()
    {
        Log.Initialize(new NoOpLogger());
        DistroMethodResolutionDiagnostics.ResetForTesting();
        DistroConfigMetrics.ResetForTesting();
    }

    private static DistroInstrumentationSite Site(ulong functionId = 1ul) =>
        new DistroInstrumentationSite("Factory", "Metric", 0u, "Asm", "NS.Worker", "DoWork", string.Empty, functionId);

    private static DistroResolvedMethod Result(DistroResolutionOutcome outcome) =>
        new DistroResolvedMethod(outcome, false, 1ul, typeof(object), string.Empty);

    [Test]
    public void Diagnostics_ReportBeforeLoggerReady_DefersLogUntilReady_ThenLogsOnce()
    {
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(), 2);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(), 2);
        Assert.That(_logger.Messages(Level.Info), Is.Empty, "Core has not started, so the logger may be the no-op one");

        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { AmbiguousText }));

        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(), 2);
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { AmbiguousText }));
    }

    // The log lines no longer wait for the metric sink (which is only set at connect): Core start is enough.
    [Test]
    public void Diagnostics_LoggerReadyWithoutMetricSink_LogsButKeepsCountsQueued()
    {
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2);
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(2ul), 2);

        Assert.That(_logger.Messages(Level.Info), Has.Length.EqualTo(2));

        var metrics = new List<string>();
        DistroConfigMetrics.SetMetricSink(metrics.Add);
        Assert.That(metrics, Is.EqualTo(new[] { NotFoundMetric, NotFoundMetric }));
    }

    [Test]
    public void Diagnostics_LoggerNotReadyAgain_HoldsLaterLines()
    {
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        DistroMethodResolutionDiagnostics.SetLoggerReady(false);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2);
        Assert.That(_logger.Messages(Level.Info), Is.Empty);

        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { NotFoundText }));
    }

    [Test]
    public void Diagnostics_Ambiguous_LogsExactMessageOnce()
    {
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(), 2);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(), 2);

        Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { AmbiguousText }));
    }

    [Test]
    public void Diagnostics_NotFound_LogsExactMessageOnce()
    {
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2);

        Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { NotFoundText }));
    }

    [Test]
    public void Diagnostics_LogIsOncePerSiteAndOutcome()
    {
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(1ul), 2);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(2ul), 2);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(1ul), 2);

        Assert.That(_logger.Messages(Level.Info), Has.Length.EqualTo(3));
    }

    [TestCase(DistroResolutionOutcome.Resolved)]
    [TestCase(DistroResolutionOutcome.AmbiguousAgreeing)]
    public void Diagnostics_Resolved_ReportsNothing(DistroResolutionOutcome outcome)
    {
        var metrics = new List<string>();
        DistroConfigMetrics.SetMetricSink(metrics.Add);
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);

        DistroMethodResolutionDiagnostics.Report(Result(outcome), Site(), 2);

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Info), Is.Empty);
            Assert.That(metrics, Is.Empty);
        });
    }

    [Test]
    public void Diagnostics_WithSink_CountsEveryReportThroughTheConfigMetricsQueue()
    {
        var metrics = new List<string>();
        DistroConfigMetrics.SetMetricSink(metrics.Add);

        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(), 2);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(), 2);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2);

        Assert.That(metrics, Is.EqualTo(new[] { AmbiguousMetric, AmbiguousMetric, NotFoundMetric }));
    }

    [Test]
    public void Diagnostics_CountsBeforeSinkSet_AreFlushedOnSet()
    {
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(), 2);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.AmbiguousDisagreeing), Site(), 2);
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2);

        var metrics = new List<string>();
        DistroConfigMetrics.SetMetricSink(metrics.Add);

        Assert.That(metrics.Count(m => m.EndsWith("/Ambiguous")), Is.EqualTo(2));
        Assert.That(metrics.Count(m => m.EndsWith("/NotFound")), Is.EqualTo(1));
    }

    [Test]
    public void Diagnostics_ConcurrentReports_LogOnce()
    {
        var metrics = new ConcurrentQueue<string>();
        DistroConfigMetrics.SetMetricSink(metrics.Enqueue);
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);

        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
            DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2));

        Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { NotFoundText }));
        Assert.That(metrics, Has.Count.EqualTo(64));
    }

    [Test]
    public void Diagnostics_LoggerThrows_StillCountsAndDoesNotThrow()
    {
        var metrics = new List<string>();
        DistroConfigMetrics.SetMetricSink(metrics.Add);
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        Log.Initialize(new ThrowingInfoLogger());

        Assert.DoesNotThrow(() => DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2));
        Assert.That(metrics, Is.EqualTo(new[] { NotFoundMetric }));
    }

    [Test]
    public void Diagnostics_LoggerThrowsOnFlush_DoesNotThrow()
    {
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2);
        Log.Initialize(new ThrowingInfoLogger());

        Assert.DoesNotThrow(() => DistroMethodResolutionDiagnostics.SetLoggerReady(true));
    }

    [Test]
    public void Diagnostics_NullSite_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), null, 2));
    }

    [Test]
    public void ResetForTesting_ForgetsHeldLinesAndReadiness()
    {
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2);
        DistroMethodResolutionDiagnostics.ResetForTesting();
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        Assert.That(_logger.Messages(Level.Info), Is.Empty);

        DistroMethodResolutionDiagnostics.ResetForTesting();
        DistroMethodResolutionDiagnostics.Report(Result(DistroResolutionOutcome.NotFound), Site(), 2);
        Assert.That(_logger.Messages(Level.Info), Is.Empty, "not ready after reset");
    }

    private sealed class ThrowingInfoLogger : ILogger
    {
        public bool IsDebugEnabled => true;
        public bool IsErrorEnabled => true;
        public bool IsFinestEnabled => true;
        public bool IsInfoEnabled => true;
        public bool IsWarnEnabled => true;

        public void Debug(Exception exception, string message, params object[] args) { }
        public void Debug(string message, params object[] args) { }
        public void Error(Exception exception, string message, params object[] args) { }
        public void Error(string message, params object[] args) { }
        public void Finest(Exception exception, string message, params object[] args) { }
        public void Finest(string message, params object[] args) { }
        public void Info(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Info(string message, params object[] args) => throw new InvalidOperationException("logger closed");
        public void Warn(Exception exception, string message, params object[] args) { }
        public void Warn(string message, params object[] args) { }
        public bool IsEnabledFor(Level level) => true;
        public void Log(Level level, string message) => throw new InvalidOperationException("logger closed");
        public void Log(Level level, Exception ex, string message) => throw new InvalidOperationException("logger closed");
    }
}
