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
/// OTLP <c>/v1/profiles</c> endpoint -- but because these host tests run against the real staging collector,
/// they are <b>log-based</b>: they assert the built-profile summary line (Debug), the opt-in gzip+base64
/// payload line (Finest), and correlation from the decoded payload's linkTable, not a received payload at the
/// collector. Whether the POST is actually accepted depends on the target endpoint/account being reachable
/// from the test host, so assertions key off the built-payload log lines rather than send success. The drain
/// also reports supportability metrics. Independent receiver-side validation of the actual OTLP protobuf
/// bytes lives in <see cref="ContinuousProfilingOtlpReceiverTests"/>, which runs against the mock collector.
///
/// Continuous profiling is enabled purely by configuration -- the session starts at agent initialization
/// (<c>AgentManager.StartIfEnabled</c>) when the config/env flag is set, with no collector command required.
/// We enable it via the environment overrides so no ad-hoc config XML is written.
///
/// Trace/span correlation IS log-observable via the opt-in payload line: each sample's linkTable entry
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
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Session started; sampling every (\d+) ms, draining every (\d+) ms\.";

    // Built-profile summary logged on every drain; capture the byte count (group 2) to assert a non-empty
    // profile was built. Logged regardless of whether ingest accepts the POST.
    private static readonly string BuiltProfileLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"\[ContinuousProfiling\] Posting profile \((\w+)\); (\d+) bytes to (\S+)\.";

    // A trace-id in a linkTable entry. The diagnostic log rewrites the proto `bytes` id from base64 to
    // lowercase hex (16 bytes -> 32 hex chars), so the reserved "no link" entry is 32 zeros; any other value
    // proves a sample was correlated to a live transaction/span.
    private const string ZeroTraceIdHex = "00000000000000000000000000000000";
    private static readonly System.Text.RegularExpressions.Regex TraceIdInJsonRegex =
        new System.Text.RegularExpressions.Regex(@"""traceId"":""([0-9a-f]{32})""");

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
                // Finest so the payload line is emitted; faster metrics cycle so the drain supportability
                // metrics harvest within the test window (default cycle is 60s).
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);

                // Enable continuous profiling via the environment overrides (never ad-hoc config XML).
                _fixture.EnvironmentVariables["NEW_RELIC_PROFILING_ENABLED"] = "true";
                // Opt in to the gzip+base64 payload line (off by default; the line carries a placeholder otherwise).
                _fixture.EnvironmentVariables["NEW_RELIC_PROFILING_LOG_PAYLOAD"] = "true";
                _fixture.EnvironmentVariables["NEW_RELIC_PROFILING_SAMPLING_INTERVAL_MS"] = SamplingIntervalMs.ToString();
            },
            exerciseApplication: () =>
            {
                // The session starts at agent init; confirm it before waiting on drain output.
                _fixture.AgentLog.WaitForLogLine(SessionStartedLogLineRegex, TimeSpan.FromMinutes(1));

                // Wait for at least one drain to build a profile.
                _fixture.AgentLog.WaitForLogLine(BuiltProfileLogLineRegex, TimeSpan.FromMinutes(2));

                // Best-effort wait for the payload line; it only appears when a non-empty profile
                // was built. Don't fail the whole run here if it's slow -- the JSON fact asserts it directly.
                _fixture.AgentLog.TryGetLogLines(ContinuousProfilingPayloadLog.LogLineRegex);

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
            () => Assert.NotNull(builtProfile),
            () => Assert.True(builtProfile != null && int.Parse(builtProfile.Groups[2].Value) > 0, "Built profile reported zero bytes.")
        );
    }

    [Fact]
    public void ContinuousProfilingLogsBuiltProfileJsonWhenPayloadLoggingIsOptedIn()
    {
        // With NEW_RELIC_PROFILING_LOG_PAYLOAD=true, each drain logs the built profile at Finest as
        // gzip+base64 of the protobuf-JSON POSTed to the collector. A matching line is the log-observable
        // evidence that a profile payload was produced. The decoded blob must be the real OTLP profile request.
        var payloadMatches = _fixture.AgentLog.WaitForLogLines(ContinuousProfilingPayloadLog.LogLineRegex, TimeSpan.FromSeconds(30)).ToArray();
        var json = ContinuousProfilingPayloadLog.DecodeToJson(payloadMatches[0].Groups[1].Value);

        NrAssert.Multiple(
            () => Assert.Contains("resourceProfiles", json),
            () => Assert.Contains("dictionary", json)
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
        // is what makes a captured sample's trace/span link reliably observable. Across all drained (decoded)
        // payloads, at least one linkTable entry must carry a non-zero trace id -- proof a sample
        // was correlated to the live transaction.
        var payloadMatches = _fixture.AgentLog.WaitForLogLines(ContinuousProfilingPayloadLog.LogLineRegex, TimeSpan.FromSeconds(30)).ToArray();

        var correlatedTraceIds = payloadMatches
            .SelectMany(m => TraceIdInJsonRegex.Matches(ContinuousProfilingPayloadLog.DecodeToJson(m.Groups[1].Value)).Cast<System.Text.RegularExpressions.Match>())
            .Select(tid => tid.Groups[1].Value)
            .Where(tid => tid != ZeroTraceIdHex)
            .ToArray();

        NrAssert.Multiple(
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

public class ContinuousProfilingTestsCoreLatestX86 : ContinuousProfilingTestsBase<ConsoleDynamicMethodFixtureCoreLatestX86>
{
    public ContinuousProfilingTestsCoreLatestX86(ConsoleDynamicMethodFixtureCoreLatestX86 fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
