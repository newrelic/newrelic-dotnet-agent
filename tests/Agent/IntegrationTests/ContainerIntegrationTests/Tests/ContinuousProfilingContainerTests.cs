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
/// Linux-container coverage for continuous profiling, exercising the native Linux sampler
/// (SuspendRuntime/DoStackSnapshot + /proc thread-name resolution) that host-run tests never touch.
/// Assertions here are log-based (built-profile summary + opt-in gzip+base64 payload line): this container tier connects
/// to a real collector, so nothing checks the collector side. Independent receiver-side validation of the
/// actual OTLP protobuf bytes lives in the host-run ContinuousProfilingOtlpReceiverTests (mock collector).
/// The fixture burns CPU synchronously on the request thread so the sampler reliably captures an active
/// trace/span for correlation assertions against the decoded payload's linkTable.
/// </summary>
[Trait("TestArea", "ContinuousProfiling")]
public abstract class ContinuousProfilingContainerTest<T> : NewRelicIntegrationTest<T> where T : ContinuousProfilingContainerTestFixtureBase
{
    // Must match the interval baked into Dockerfile.continuousprofiling
    // (NEW_RELIC_PROFILING_SAMPLING_INTERVAL_MS).
    private const int SamplingIntervalMs = 1000;

    private readonly T _fixture;

    private static readonly string SessionStartedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Session started; sampling every (\d+) ms, draining every (\d+) ms\.";

    private static readonly string BuiltProfileLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"\[ContinuousProfiling\] Posting profile \((\w+)\); (\d+) bytes to (\S+)\.";

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
                // Finest so the opt-in payload line (with the correlation link) is emitted (the Dockerfile sets
                // NEW_RELIC_PROFILING_LOG_PAYLOAD=true); faster
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
        // all drained (decoded) payloads, at least one linkTable entry must carry a non-zero trace id.
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
}

// TestArea=ContinuousProfiling (inherited from the abstract base) is this class's only CI selector --
// see the matching "ContinuousProfiling" matrix entries in linux_container_tests.yml. No Distro trait:
// per tests/CLAUDE.md, Distro is reserved for OS-compatibility smoke tests, not functional coverage.
[Trait("Architecture", "amd64")]
public class ContinuousProfilingUbuntuX64ContainerTest : ContinuousProfilingContainerTest<ContinuousProfilingUbuntuX64ContainerTestFixture>
{
    public ContinuousProfilingUbuntuX64ContainerTest(ContinuousProfilingUbuntuX64ContainerTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Architecture", "arm64")]
public class ContinuousProfilingUbuntuArm64ContainerTest : ContinuousProfilingContainerTest<ContinuousProfilingUbuntuArm64ContainerTestFixture>
{
    public ContinuousProfilingUbuntuArm64ContainerTest(ContinuousProfilingUbuntuArm64ContainerTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

// Alpine (musl) x64: the shipped glibc .so is what runs on every distro (see tests/CLAUDE.md "glibc vs
// musl"), and on x64 it loads under musl, so this validates CP end to end on the musl runtime. No arm64
// Alpine variant: the arm64 glibc .so imports __stack_chk_guard from ld-linux-aarch64.so.1, which musl
// does not provide, so the profiler (and the whole agent) cannot load there.
[Trait("Architecture", "amd64")]
public class ContinuousProfilingAlpineX64ContainerTest : ContinuousProfilingContainerTest<ContinuousProfilingAlpineX64ContainerTestFixture>
{
    public ContinuousProfilingAlpineX64ContainerTest(ContinuousProfilingAlpineX64ContainerTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
