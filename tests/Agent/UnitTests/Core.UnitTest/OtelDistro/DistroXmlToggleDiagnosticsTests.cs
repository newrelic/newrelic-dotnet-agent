// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using static NewRelic.Agent.Core.OtelDistro.InstrumentationDefinitionsTableTestData;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroXmlToggleDiagnosticsTests
{
    private const string Matches = "NR instrumentation XML matches the distro's compiled instrumentation (3 shipped instrumentation files are not compiled into the distro yet; edits to them have no effect).";

    private static readonly string[] NoFiles = new string[0];
    private static readonly DistroXmlUnsupportedPoint[] NoUnsupported = new DistroXmlUnsupportedPoint[0];
    private static readonly bool[] AllOn = { true, true, true, true, true };

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

    // Sites after the reader's stable sort by assembly: 0 "Run" (point 1), 1 and 2 "Wide" (point 2, an overload-wide point
    // with two records), 3 "Read" (point 3), 4 "Read" again under another factory (point 4, an R4 pair).
    private static DistroInstrumentationSites Sites()
    {
        var table = Build(
            new Row { Assembly = "A1", Type = "NS.Foo", Method = "Run", Signature = new[] { "_", "System.String" }, ArgumentSignature = "System.String", TracerFactory = "F1", PointKey = 1, FunctionId = 1 },
            new Row { Assembly = "A2", Type = "NS.Foo", Method = "Wide", Signature = new[] { "_" }, ArgumentSignature = "", TracerFactory = "F2", PointKey = 2, FunctionId = 2 },
            new Row { Assembly = "A2", Type = "NS.Foo", Method = "Wide", Signature = new[] { "_", "_" }, ArgumentSignature = "", TracerFactory = "F2", PointKey = 2, FunctionId = 3 },
            new Row { Assembly = "A3", Type = "NS.Foo", Method = "Read", Signature = new[] { "_" }, ArgumentSignature = "", TracerFactory = "F3", PointKey = 3, FunctionId = 4 },
            new Row { Assembly = "A3", Type = "NS.Foo", Method = "Read", Signature = new[] { "_" }, ArgumentSignature = "", TracerFactory = "F3Async", PointKey = 4, FunctionId = 5 });
        var ok = InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out _, out var failure);
        Assert.That(ok, Is.True, () => failure);
        return sites;
    }

    private static DistroXmlEffectiveSetResult Result(bool[] on, string[] parsedFiles = null, DistroXmlUnsupportedPoint[] unsupported = null, int hashOnly = 0, string[] missing = null) =>
        new DistroXmlEffectiveSetResult(false, null, null, on, parsedFiles, null, null, missing, unsupported, null, hashOnly);

    private static DistroXmlUnsupportedPoint Unsupported(int n) => new DistroXmlUnsupportedPoint("X.xml", "NS.T" + n, "M" + n, "System.String", "Fac", (ulong)n);

    private static void Report(DistroInstrumentationSites sites, bool[] before, DistroXmlEffectiveSetResult result, IReadOnlyList<string> changedFiles = null, IReadOnlyList<DistroXmlUnsupportedPoint> newUnsupported = null) =>
        DistroXmlToggleDiagnostics.ReportApplied(sites, before, result.On, result, changedFiles ?? NoFiles, newUnsupported ?? result.Unsupported);

    private int Count(string metric) => _metrics.Count(m => m == metric);

    [Test]
    public void ReportApplied_NoChange_LogsOneInfoLineWithHashOnlyCount()
    {
        Report(Sites(), AllOn, Result(AllOn, hashOnly: 3));

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { Matches }));
            Assert.That(_logger.Messages(Level.Warn), Is.Empty);
            Assert.That(_metrics, Is.Empty);
        });
    }

    [TestCase("parsed")]
    [TestCase("missing")]
    [TestCase("changed")]
    [TestCase("unsupported")]
    public void ReportApplied_NoPointChangedButFilesDiffer_SaysWhichInsteadOfMatches(string why)
    {
        var result = Result(
            AllOn,
            parsedFiles: why == "parsed" ? new[] { "A.xml" } : null,
            unsupported: why == "unsupported" ? new[] { Unsupported(1) } : null,
            hashOnly: 3,
            missing: why == "missing" ? new[] { "B.xml" } : null);

        Report(Sites(), AllOn, result, why == "changed" ? new[] { "C.xml (unreadable)" } : null, NoUnsupported);

        var expectedFiles = why == "changed" ? "C.xml (unreadable)" : "none";
        var expectedUnsupported = why == "unsupported" ? 1 : 0;
        Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { "NR instrumentation XML applied: no compiled points changed, " + expectedUnsupported + " not supported, from files: " + expectedFiles }));
    }

    [Test]
    public void ReportApplied_Changes_LogsSummaryAndOneLinePerChangedPointAndOneMetricPerPoint()
    {
        var before = new[] { true, true, true, false, false };
        var after = new[] { false, false, false, true, true };

        Report(Sites(), before, Result(after), new[] { "A.xml", "Extra.xml" });

        var lines = _logger.Messages(Level.Info);
        Assert.Multiple(() =>
        {
            Assert.That(lines, Is.EqualTo(new[]
            {
                "NR instrumentation XML applied: 2 points turned off, 2 turned on, 0 not supported, from files: A.xml, Extra.xml",
                "NR instrumentation XML turned off NS.Foo.Run(System.String) [F1].",
                "NR instrumentation XML turned off NS.Foo.Wide(*) [F2].",
                "NR instrumentation XML turned on NS.Foo.Read() [F3].",
                "NR instrumentation XML turned on NS.Foo.Read() [F3Async].",
            }));
            Assert.That(Count(DistroXmlToggleDiagnostics.PointsDisabledMetricName), Is.EqualTo(2), "one per point, not per site");
            Assert.That(Count(DistroXmlToggleDiagnostics.PointsEnabledMetricName), Is.EqualTo(2));
            Assert.That(lines.Count(l => l.Contains(" turned off ")), Is.EqualTo(Count(DistroXmlToggleDiagnostics.PointsDisabledMetricName)), "summary count == line count");
            Assert.That(Count(DistroXmlToggleDiagnostics.UnsupportedMetricName), Is.EqualTo(0));
        });
    }

    [Test]
    public void ReportApplied_OverloadWidePointPartlyChanged_OneLine()
    {
        // Only one of the two records changed (partial registration state): still one line, and still "(*)" because the
        // point has two records in the table.
        Report(Sites(), AllOn, Result(new[] { true, false, true, true, true }));

        Assert.That(_logger.Messages(Level.Info).Skip(1), Is.EqualTo(new[] { "NR instrumentation XML turned off NS.Foo.Wide(*) [F2]." }));
    }

    [Test]
    public void ReportApplied_SingleRecordPointChanged_ShowsItsOwnSignatureNotWide()
    {
        Report(Sites(), AllOn, Result(new[] { false, true, true, true, true }));

        Assert.That(_logger.Messages(Level.Info).Skip(1), Is.EqualTo(new[] { "NR instrumentation XML turned off NS.Foo.Run(System.String) [F1]." }));
    }

    [Test]
    public void ReportApplied_Unsupported_WarnsPerNewPointCountsNewOnesSummaryCountsAll()
    {
        var result = Result(AllOn, unsupported: new[] { Unsupported(1), Unsupported(2) });

        Report(Sites(), AllOn, result, newUnsupported: new[] { Unsupported(2) });

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[]
            {
                "NR instrumentation XML X.xml names NS.T2.M2(System.String) [Fac], which is not part of the distro's compiled instrumentation; it will not be instrumented (custom instrumentation is not supported in distro mode yet).",
            }));
            Assert.That(Count(DistroXmlToggleDiagnostics.UnsupportedMetricName), Is.EqualTo(1));
            Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { "NR instrumentation XML applied: no compiled points changed, 2 not supported, from files: none" }));
        });
    }

    [Test]
    public void ReportApplied_UnsupportedWithChanges_CountsThemInTheSummary()
    {
        Report(Sites(), AllOn, Result(new[] { false, true, true, true, true }, new[] { "A.xml" }, new[] { Unsupported(1) }), new[] { "A.xml" });

        Assert.That(_logger.Messages(Level.Info)[0], Is.EqualTo("NR instrumentation XML applied: 1 points turned off, 0 turned on, 1 not supported, from files: A.xml"));
    }

    [Test]
    public void ReportApplied_MoreThanTwentyUnsupported_WarnsTwentyThenOverflowLine_ButCountsAll()
    {
        Report(Sites(), AllOn, Result(AllOn, unsupported: Enumerable.Range(1, 25).Select(Unsupported).ToArray()));

        var warnings = _logger.Messages(Level.Warn);
        Assert.Multiple(() =>
        {
            Assert.That(warnings, Has.Length.EqualTo(21));
            Assert.That(warnings[19], Does.Contain("NS.T20.M20"));
            Assert.That(warnings[20], Is.EqualTo("... and 5 more NR instrumentation XML points are not supported in distro mode."));
            Assert.That(Count(DistroXmlToggleDiagnostics.UnsupportedMetricName), Is.EqualTo(25));
        });
    }

    [Test]
    public void ReportApplied_ExactlyTwentyUnsupported_NoOverflowLine()
    {
        Report(Sites(), AllOn, Result(AllOn, unsupported: Enumerable.Range(1, 20).Select(Unsupported).ToArray()));

        Assert.That(_logger.Messages(Level.Warn), Has.Length.EqualTo(20));
    }

    [Test]
    public void ReportApplied_LoggerThrows_MetricsStillRecordedAndNothingThrown()
    {
        Log.Initialize(new ThrowingLogger());

        Assert.DoesNotThrow(() => Report(Sites(), AllOn, Result(new[] { false, true, true, true, true }, unsupported: new[] { Unsupported(1) })));

        Assert.Multiple(() =>
        {
            Assert.That(Count(DistroXmlToggleDiagnostics.PointsDisabledMetricName), Is.EqualTo(1));
            Assert.That(Count(DistroXmlToggleDiagnostics.UnsupportedMetricName), Is.EqualTo(1));
        });
    }

    [Test]
    public void ReportApplied_BadArguments_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => DistroXmlToggleDiagnostics.ReportApplied(Sites(), AllOn, AllOn, null, NoFiles, NoUnsupported));
    }

    [Test]
    public void ReportRejected_LogsErrorAndCountsParseError()
    {
        var rejected = new DistroXmlEffectiveSetResult(true, "A.xml", "Malformed instrumentation XML: oops", null, null, null, null, null, null, null, 0);

        DistroXmlToggleDiagnostics.ReportRejected(rejected);

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Error), Is.EqualTo(new[] { "NR instrumentation XML A.xml could not be parsed (Malformed instrumentation XML: oops); instrumentation will not be refreshed." }));
            Assert.That(_metrics, Is.EqualTo(new[] { "Supportability/Dotnet/OtelDistro/Xml/ParseError" }));
        });
    }

    [Test]
    public void ReportSkippedAtStartup_LogsErrorNamingTheFileAndCountsParseError()
    {
        DistroXmlToggleDiagnostics.ReportSkippedAtStartup("A.xml", "Malformed instrumentation XML: oops");

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Error), Is.EqualTo(new[] { "NR instrumentation XML A.xml could not be parsed (Malformed instrumentation XML: oops); it was skipped (a shipped file keeps the distro's compiled defaults), and live instrumentation updates will not be applied until it is fixed." }));
            Assert.That(_metrics, Is.EqualTo(new[] { DistroXmlToggleDiagnostics.ParseErrorMetricName }));
        });
    }

    [Test]
    public void ReportUnreadable_LogsWarnAndCountsUnreadable()
    {
        DistroXmlToggleDiagnostics.ReportUnreadable("A.xml", "locked");

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "NR instrumentation XML A.xml could not be read (locked); using the distro's compiled defaults for it." }));
            Assert.That(_metrics, Is.EqualTo(new[] { "Supportability/Dotnet/OtelDistro/Xml/Unreadable" }));
        });
    }

    [Test]
    public void ReportUnreadableNotRefreshed_LogsWarnAndCountsUnreadable()
    {
        DistroXmlToggleDiagnostics.ReportUnreadableNotRefreshed("A.xml", "locked");

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "NR instrumentation XML A.xml could not be read (locked); instrumentation was not refreshed, and live changes stay blocked until the file can be read again." }));
            Assert.That(_metrics, Is.EqualTo(new[] { DistroXmlToggleDiagnostics.UnreadableMetricName }));
        });
    }

    [Test]
    public void ReportFolderMissing_LogsWarnNamingTheFolderAndCountsFolderMissing()
    {
        DistroXmlToggleDiagnostics.ReportFolderMissing("/nr/extensions");

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "NR instrumentation XML folder /nr/extensions was not found; using the distro's compiled instrumentation defaults." }));
            Assert.That(_metrics, Is.EqualTo(new[] { "Supportability/Dotnet/OtelDistro/Xml/FolderMissing" }));
        });
    }

    [Test]
    public void ReportFolderMissing_NoFolder_SaysSo()
    {
        DistroXmlToggleDiagnostics.ReportFolderMissing(null);

        Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "NR instrumentation XML folder (no extensions directory) was not found; using the distro's compiled instrumentation defaults." }));
    }

    [Test]
    public void ReportRegisterUnavailable_FirstErrorThenDebug_CountedEachTime()
    {
        DistroXmlToggleDiagnostics.ReportRegisterUnavailable("OTel 1.18.0.0: EntryPointNotFoundException: x", 3, alreadyReported: false);
        DistroXmlToggleDiagnostics.ReportRegisterUnavailable("OTel 1.18.0.0: EntryPointNotFoundException: x", 3, alreadyReported: true);

        const string expected = "NR instrumentation could not be turned on at run time: OTel run-time registration is unavailable (OTel 1.18.0.0: EntryPointNotFoundException: x); 3 points stay off.";
        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Error), Is.EqualTo(new[] { expected }));
            Assert.That(_logger.Messages(Level.Debug), Is.EqualTo(new[] { expected }));
            Assert.That(_metrics, Is.EqualTo(new[] { DistroXmlToggleDiagnostics.RegisterUnavailableMetricName, DistroXmlToggleDiagnostics.RegisterUnavailableMetricName }));
        });
    }

    [Test]
    public void ReportRegisteredAtRunTime_LogsRestartRecommendedAndCounts()
    {
        DistroXmlToggleDiagnostics.ReportRegisteredAtRunTime(2);

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[] { "NR instrumentation XML turned on 2 points that were not registered at start-up; they are now registered with OTel in this process. Methods already compiled into their callers may stay uninstrumented until the application restarts (restart recommended)." }));
            Assert.That(_metrics, Is.EqualTo(new[] { DistroXmlToggleDiagnostics.RegisteredAtRunTimeMetricName }));
        });
    }

    [Test]
    public void SingleReports_LoggerThrows_MetricStillRecordedAndNothingThrown()
    {
        Log.Initialize(new ThrowingLogger());
        var rejected = new DistroXmlEffectiveSetResult(true, "A.xml", "bad", null, null, null, null, null, null, null, 0);

        Assert.DoesNotThrow(() =>
        {
            DistroXmlToggleDiagnostics.ReportRejected(rejected);
            DistroXmlToggleDiagnostics.ReportSkippedAtStartup("A.xml", "bad");
            DistroXmlToggleDiagnostics.ReportUnreadable("A.xml", "locked");
            DistroXmlToggleDiagnostics.ReportUnreadableNotRefreshed("A.xml", "locked");
            DistroXmlToggleDiagnostics.ReportRegisterUnavailable("x", 1, alreadyReported: false);
            DistroXmlToggleDiagnostics.ReportRegisterUnavailable("x", 1, alreadyReported: true);
            DistroXmlToggleDiagnostics.ReportRegisteredAtRunTime(1);
            DistroXmlToggleDiagnostics.ReportFolderMissing("x");
        });

        Assert.That(_metrics, Is.EqualTo(new[]
        {
            DistroXmlToggleDiagnostics.ParseErrorMetricName,
            DistroXmlToggleDiagnostics.ParseErrorMetricName,
            DistroXmlToggleDiagnostics.UnreadableMetricName,
            DistroXmlToggleDiagnostics.UnreadableMetricName,
            DistroXmlToggleDiagnostics.RegisterUnavailableMetricName,
            DistroXmlToggleDiagnostics.RegisterUnavailableMetricName,
            DistroXmlToggleDiagnostics.RegisteredAtRunTimeMetricName,
            DistroXmlToggleDiagnostics.FolderMissingMetricName,
        }));
    }

    [Test]
    public void MetricNames_AreTheSpecifiedStrings()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DistroXmlToggleDiagnostics.PointsDisabledMetricName, Is.EqualTo("Supportability/Dotnet/OtelDistro/Xml/PointsDisabled"));
            Assert.That(DistroXmlToggleDiagnostics.PointsEnabledMetricName, Is.EqualTo("Supportability/Dotnet/OtelDistro/Xml/PointsEnabled"));
            Assert.That(DistroXmlToggleDiagnostics.UnsupportedMetricName, Is.EqualTo("Supportability/Dotnet/OtelDistro/Xml/Unsupported"));
            Assert.That(DistroXmlToggleDiagnostics.ParseErrorMetricName, Is.EqualTo("Supportability/Dotnet/OtelDistro/Xml/ParseError"));
            Assert.That(DistroXmlToggleDiagnostics.RegisterUnavailableMetricName, Is.EqualTo("Supportability/Dotnet/OtelDistro/Xml/RegisterUnavailable"));
            Assert.That(DistroXmlToggleDiagnostics.RegisteredAtRunTimeMetricName, Is.EqualTo("Supportability/Dotnet/OtelDistro/Xml/RegisteredAtRunTime"));
            Assert.That(DistroXmlToggleDiagnostics.UnreadableMetricName, Is.EqualTo("Supportability/Dotnet/OtelDistro/Xml/Unreadable"));
            Assert.That(DistroXmlToggleDiagnostics.FolderMissingMetricName, Is.EqualTo("Supportability/Dotnet/OtelDistro/Xml/FolderMissing"));
        });
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
