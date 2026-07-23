// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

/// <summary>
/// End-to-end coverage for continuous profiling. Each drain now POSTs the built profile to the configured
/// OTLP <c>/v1/profiles</c> endpoint -- but these host tests remain <b>log-based only</b>: they assert the
/// built-profile summary line, the protobuf-JSON payload dump (both Debug), and correlation from the dump's
/// linkTable, not a received payload at the collector. Whether the POST is actually accepted depends on the
/// target endpoint/account being reachable from the test host, so assertions key off the built-payload log
/// lines rather than send success. The drain also reports supportability metrics.
///
/// Continuous profiling is enabled purely by configuration -- the session starts at agent initialization
/// (<c>AgentManager.StartIfEnabled</c>) when the config/env flag is set, with no collector command required.
/// We enable it via the environment overrides so no ad-hoc config XML is written.
///
/// Trace/span correlation IS log-observable via the Debug payload dump: each sample's linkTable entry
/// carries a <c>traceId</c> that is non-zero (i.e. not all-zero hex) whenever that sample was captured while
/// a transaction/span was active. We exercise correlation by running the sampled work inside instrumented
/// transactions/segments (<c>ContinuousProfilingExerciser</c>) and assert that at least one linkTable entry
/// carries a non-zero trace id (<see cref="TraceIdInJsonRegex"/>).
/// </summary>
public abstract class ContinuousProfilingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    // The configured interval. 1000 ms is the minimum the agent will clamp to, and small enough that a short
    // exercise window spans several capture+drain cycles.
    private const int SamplingIntervalMs = 1000;

    protected readonly TFixture _fixture;

    private static readonly string SessionStartedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Session started; draining every (\d+) ms\.";

    // Built-profile summary logged on every drain; capture the byte count (group 2) to assert a non-empty
    // profile was built. Logged regardless of whether ingest accepts the POST.
    private static readonly string BuiltProfileLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"\[ContinuousProfiling\] Posting profile \((\w+)\); (\d+) bytes to (\S+)\.";

    // Each drain logs the built profile at Debug as compact protobuf-JSON in the same shape POSTed to the
    // collector (mirrors HttpCollectorWire): `Request(<guid>): Invoked "continuous_profiling" with : {...}`.
    // Group 1 captures the single-line JSON blob. A matching line is the observable evidence that a profile
    // was built (the summary line reports only "built"/"empty" + a byte count).
    private static readonly string ProfileJsonLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"Request\(.+?\): Invoked ""continuous_profiling"" with : (\{.*\})";

    // A trace-id in a linkTable entry. The diagnostic log rewrites the proto `bytes` id from base64 to
    // lowercase hex (16 bytes -> 32 hex chars), so the reserved "no link" entry is 32 zeros; any other value
    // proves a sample was correlated to a live transaction/span.
    private const string ZeroTraceIdHex = "00000000000000000000000000000000";
    private static readonly System.Text.RegularExpressions.Regex TraceIdInJsonRegex =
        new System.Text.RegularExpressions.Regex(@"""traceId"":""([0-9a-f]{32})""");

    // Emitted by the thread profiler's forward guard if a thread-profiling start is attempted while CP is on.
    private static readonly string ThreadProfilingRefusedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"Thread profiling start refused: continuous profiling is active\.";

    private const string DrainMetricName = "Supportability/DotNET/ContinuousProfiling/Drain";
    private const string SamplesMetricName = "Supportability/DotNET/ContinuousProfiling/Samples";

    protected ContinuousProfilingTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.SetTimeout(TimeSpan.FromMinutes(3));

        // Run CPU-busy work synchronously and inline on a SINGLE thread, inside one instrumented
        // [Transaction]/[Trace] method, for long enough to span several sampling intervals. SetTraceContext
        // is pushed at the wrapper boundary keyed by the calling OS thread only -- it is never propagated to
        // spawned worker threads -- so keeping the busy loop on the calling (traced) thread itself is what
        // makes a captured sample's trace/span link reliably observable (see RunCorrelatedBusyWork).
        _fixture.AddCommand($"ContinuousProfilingExerciser RunCorrelatedBusyWork 8");

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                // Debug (via finest) so the payload dump + correlation lines are emitted; faster metrics cycle
                // so the drain supportability metrics harvest within the test window (default cycle is 60s).
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);

                // Enable continuous profiling via the environment overrides (never ad-hoc config XML).
                _fixture.EnvironmentVariables["NEW_RELIC_CONTINUOUS_PROFILING_ENABLED"] = "true";
                _fixture.EnvironmentVariables["NEW_RELIC_CONTINUOUS_PROFILING_SAMPLING_INTERVAL_MS"] = SamplingIntervalMs.ToString();
            },
            exerciseApplication: () =>
            {
                // The session starts at agent init; confirm it before waiting on drain output.
                _fixture.AgentLog.WaitForLogLine(SessionStartedLogLineRegex, TimeSpan.FromMinutes(1));

                // Wait for at least one drain to build a profile.
                _fixture.AgentLog.WaitForLogLine(BuiltProfileLogLineRegex, TimeSpan.FromMinutes(2));

                // Best-effort wait for the Debug JSON payload line; it only appears when a non-empty profile
                // was built. Don't fail the whole run here if it's slow -- the JSON fact asserts it directly.
                _fixture.AgentLog.TryGetLogLines(ProfileJsonLogLineRegex);

                // Give the metric harvest a chance to ship the drain supportability metrics.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void ContinuousProfilingSessionStartsWithConfiguredInterval()
    {
        var match = _fixture.AgentLog.WaitForLogLine(SessionStartedLogLineRegex, TimeSpan.FromSeconds(30));
        var reportedInterval = int.Parse(match.Groups[1].Value);

        Assert.Equal(SamplingIntervalMs, reportedInterval);
    }

    [Fact]
    public void ContinuousProfilingBuildsNonEmptyProfile()
    {
        // Multiple drains may occur; take the first that reports a non-empty ("built") profile.
        var matches = _fixture.AgentLog.WaitForLogLines(BuiltProfileLogLineRegex, TimeSpan.FromSeconds(30)).ToArray();

        var builtProfile = matches.FirstOrDefault(m => m.Groups[1].Value == "built");

        NrAssert.Multiple(
            () => Assert.NotEmpty(matches),
            () => Assert.NotNull(builtProfile),
            () => Assert.True(int.Parse(builtProfile.Groups[2].Value) > 0, "Built profile reported zero bytes.")
        );
    }

    [Fact]
    public void ContinuousProfilingLogsBuiltProfileJsonAtDebug()
    {
        // At Debug, each drain logs the built profile as compact protobuf-JSON in the same shape POSTed to
        // the collector (mirrors HttpCollectorWire). A matching line is the log-observable evidence that a
        // profile payload was produced. The captured blob must be the real OTLP profile request.
        var jsonMatches = _fixture.AgentLog.WaitForLogLines(ProfileJsonLogLineRegex, TimeSpan.FromSeconds(30)).ToArray();

        NrAssert.Multiple(
            () => Assert.NotEmpty(jsonMatches),
            () => Assert.Contains("resourceProfiles", jsonMatches[0].Groups[1].Value),
            () => Assert.Contains("dictionary", jsonMatches[0].Groups[1].Value)
        );
    }

    [Fact]
    public void ContinuousProfilingLogsNonZeroTraceSpanLinkForSampleTakenDuringTransaction()
    {
        // The exerciser's busy work runs synchronously, inline, on a SINGLE thread inside a
        // [Transaction]/[Trace]-instrumented method (ContinuousProfilingExerciser.RunCorrelatedBusyWork ->
        // CorrelatedBusyTransaction -> CorrelatedBurnCpu) for several seconds, spanning multiple sampling
        // intervals, without ever handing the work off to another thread. SetTraceContext is pushed at the
        // wrapper boundary keyed by the calling OS thread only, so keeping the busy loop on that same thread
        // is what makes a captured sample's trace/span link reliably observable. Across all drained JSON
        // payloads, at least one linkTable entry must carry a non-zero (base64) trace id -- proof a sample
        // was correlated to the live transaction.
        var jsonMatches = _fixture.AgentLog.WaitForLogLines(ProfileJsonLogLineRegex, TimeSpan.FromSeconds(30)).ToArray();

        var correlatedTraceIds = jsonMatches
            .SelectMany(m => TraceIdInJsonRegex.Matches(m.Groups[1].Value).Cast<System.Text.RegularExpressions.Match>())
            .Select(tid => tid.Groups[1].Value)
            .Where(tid => tid != ZeroTraceIdHex)
            .ToArray();

        NrAssert.Multiple(
            () => Assert.NotEmpty(jsonMatches),
            () => Assert.NotEmpty(correlatedTraceIds)
        );
    }

    [Fact]
    public void ContinuousProfilingReportsDrainSupportabilityMetrics()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var drainMetric = metrics.FirstOrDefault(x => x.MetricSpec.Name == DrainMetricName);
        var samplesMetric = metrics.FirstOrDefault(x => x.MetricSpec.Name == SamplesMetricName);

        NrAssert.Multiple(
            () => Assert.NotNull(drainMetric),
            () => Assert.True(drainMetric.Values.CallCount > 0, "Drain metric call count was zero."),
            () => Assert.NotNull(samplesMetric),
            () => Assert.True(samplesMetric.Values.CallCount > 0, "Samples metric call count was zero.")
        );
    }

    [Fact]
    public void ThreadProfilingDoesNotRunWhileContinuousProfilingIsActive()
    {
        // The two profilers are mutually exclusive. A console app cannot trigger a collector-driven
        // start_profiler command (that path needs the MockNewRelic collector fixture), so we assert the
        // observable guarantee available here: while continuous profiling is active, no thread-profiling
        // session started. If a thread-profiling start had been attempted, the forward guard would have
        // logged the refusal line; the absence of a "Starting a thread profiling session" line confirms the
        // two never ran concurrently.
        var sessionStarted = _fixture.AgentLog.TryGetLogLines(SessionStartedLogLineRegex).Any();
        var threadProfilingStarted = _fixture.AgentLog.TryGetLogLines(AgentLogBase.ThreadProfileStartingLogLineRegex).Any();
        var refusalLogged = _fixture.AgentLog.TryGetLogLines(ThreadProfilingRefusedLogLineRegex).Any();

        NrAssert.Multiple(
            () => Assert.True(sessionStarted, "Continuous profiling session never started."),
            // Either no thread-profiling session ran at all, or every attempt was refused by the guard.
            () => Assert.True(!threadProfilingStarted || refusalLogged,
                "A thread-profiling session started while continuous profiling was active without being refused.")
        );
    }
}

public class ContinuousProfilingTestsCoreLatest : ContinuousProfilingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public ContinuousProfilingTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class ContinuousProfilingTestsCoreOldest : ContinuousProfilingTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public ContinuousProfilingTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
