// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

/// <summary>
/// Linux-container end-to-end coverage for continuous profiling -- the counterpart to the host-run
/// (Windows) ContinuousProfilingTests, exercising the NATIVE LINUX sampler (SuspendRuntime/DoStackSnapshot
/// + /proc thread-name resolution) which the host-run tests never touch.
///
/// Each drain now POSTs the built profile to the configured OTLP endpoint, but these container tests remain
/// LOG-BASED ONLY: they assert the built-profile summary line and the protobuf-JSON payload dump (both
/// Debug), not a received payload at the collector. Continuous profiling is enabled by configuration
/// (NEW_RELIC_CONTINUOUS_PROFILING_* env in Dockerfile.continuousprofiling) and the session starts at agent
/// init with no collector command. The fixture drives a synchronous CPU-burn endpoint on the request
/// (traced) thread so the sampler captures a thread with an active trace/span, which lets us assert
/// end-to-end trace/span correlation on Linux from the Debug JSON dump's linkTable.
/// </summary>
public abstract class ContinuousProfilingContainerTest<T> : NewRelicIntegrationTest<T> where T : ContinuousProfilingContainerTestFixtureBase
{
    // Must match the interval baked into Dockerfile.continuousprofiling
    // (NEW_RELIC_CONTINUOUS_PROFILING_SAMPLING_INTERVAL_MS).
    private const int SamplingIntervalMs = 1000;

    private readonly T _fixture;

    private static readonly string SessionStartedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Session started; draining every (\d+) ms\.";

    private static readonly string BuiltProfileLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"\[ContinuousProfiling\] Posting profile \((\w+)\); (\d+) bytes to (\S+)\.";

    // Each drain logs the built profile at Debug as compact protobuf-JSON in collector-send shape
    // (mirrors HttpCollectorWire): `Request(<guid>): Invoked "continuous_profiling" with : {...}`. It is a
    // SINGLE physical line, so the log prefix applies to the whole line and we anchor on it. Group 1 = JSON.
    private static readonly string ProfileJsonLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"Request\(.+?\): Invoked ""continuous_profiling"" with : (\{.*\})";

    // A trace id in a linkTable entry. The diagnostic log rewrites the proto `bytes` id from base64 to
    // lowercase hex (16 bytes -> 32 hex chars), so the reserved "no link" entry is 32 zeros; any other value
    // proves a correlated sample.
    private const string ZeroTraceIdHex = "00000000000000000000000000000000";
    private static readonly System.Text.RegularExpressions.Regex TraceIdInJsonRegex =
        new System.Text.RegularExpressions.Regex(@"""traceId"":""([0-9a-f]{32})""");

    private const string DrainMetricName = "Supportability/DotNET/ContinuousProfiling/Drain";
    private const string SamplesMetricName = "Supportability/DotNET/ContinuousProfiling/Samples";

    protected ContinuousProfilingContainerTest(T fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                // Debug (via finest) so the payload dump (with the correlation link) is emitted; faster
                // metrics cycle so the drain supportability metrics harvest within the test window.
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
            },
            exerciseApplication: () =>
            {
                // Session starts at agent init; confirm before waiting on drain output.
                _fixture.AgentLog.WaitForLogLine(SessionStartedLogLineRegex, TimeSpan.FromMinutes(1));

                // Exercise the burn endpoint (blocks ~8s while CPU-busy inside the web transaction).
                _fixture.ExerciseApplication();

                // At least one drain must build a profile, then a metric harvest must ship the
                // supportability metrics. On loaded runners give these generous windows.
                _fixture.AgentLog.WaitForLogLine(BuiltProfileLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1), 1);

                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(30));
            });

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
    public void ContinuousProfilingBuildsNonEmptyProfileFromNativeLinuxSampler()
    {
        var matches = _fixture.AgentLog.WaitForLogLines(BuiltProfileLogLineRegex, TimeSpan.FromSeconds(30)).ToArray();

        var builtProfile = matches.FirstOrDefault(m => m.Groups[1].Value == "built");
        Assert.NotNull(builtProfile);
        Assert.True(int.Parse(builtProfile.Groups[2].Value) > 0, "Built profile reported zero bytes.");
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
    public void ContinuousProfilingLogsNonZeroTraceSpanLinkOnLinux()
    {
        // The burn endpoint runs 8s of on-CPU work inside the instrumented web transaction, spanning
        // several 1000 ms sampling intervals, so the sampler reliably captures the request thread with an
        // active trace/span. This is the Linux-side proof of the suspend-window trace-context read. Across
        // all drained JSON payloads, at least one linkTable entry must carry a non-zero (base64) trace id.
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
}

[Trait("Architecture", "amd64")]
[Trait("Distro", "Ubuntu")]
public class ContinuousProfilingUbuntuX64ContainerTest : ContinuousProfilingContainerTest<ContinuousProfilingUbuntuX64ContainerTestFixture>
{
    public ContinuousProfilingUbuntuX64ContainerTest(ContinuousProfilingUbuntuX64ContainerTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Architecture", "arm64")]
[Trait("Distro", "Ubuntu")]
public class ContinuousProfilingUbuntuArm64ContainerTest : ContinuousProfilingContainerTest<ContinuousProfilingUbuntuArm64ContainerTestFixture>
{
    public ContinuousProfilingUbuntuArm64ContainerTest(ContinuousProfilingUbuntuArm64ContainerTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
