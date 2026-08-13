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
/// End-to-end coverage for ALLOCATION sampling: native EventPipe AllocationTick capture -> wire format ->
/// managed decode -> OTLP build -> transport, on a real running agent. Structured exactly like
/// <c>ContinuousProfilingTests</c> (its CPU/thread-sampling sibling) and, like it, <b>log-based only</b> --
/// assertions key off the built-profile summary line and the Debug protobuf-JSON payload dump, not off a
/// payload received at a collector, because whether the POST is accepted depends on the target
/// endpoint/account being reachable from the test host.
///
/// This run is deliberately ALLOCATION-ONLY: <c>NEW_RELIC_CONTINUOUS_PROFILING_ENABLED</c> is NOT set, so
/// the timer-driven thread sampler never starts. That matters twice over -- it proves allocation sampling is
/// independent of the thread-sampling flag (the two have separate config gates), and it means every profile
/// in the dumped payload can only have come from the allocation path, so <c>allocated_objects</c>/
/// <c>allocated_space</c> appearing there is unambiguous. The shared drain timer is still armed (both
/// samplers share one drain), which is why the sampling-interval env var is set: with thread sampling off it
/// serves only as the drain cadence.
///
/// Trace/span correlation is asserted the same way as in the CPU test, from the dumped payload's linkTable.
/// It carries more weight here: an allocation sample's trace/span link is a REQUIRED part of the sample
/// (captured at the allocating call site, on the allocating thread, inside the transaction), where for a
/// thread sample it is merely common.
/// </summary>
public abstract class ContinuousProfilingAllocationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    // With thread sampling off this is ONLY the shared drain cadence (there is no thread-sampling interval to
    // set). 1000 ms is the minimum the agent clamps to, so a short exercise window spans many drains -- and it
    // is load-bearing for this test's runtime, because the delivered allocation-sample rate is currently one
    // sample per drain (see MaxSamplesPerMinute), not the configured budget.
    private const int DrainIntervalMs = 1000;

    // Well above the shipped default of 200/minute so the configured budget is never the binding constraint
    // inside the test window.
    //
    // MEASURED, not assumed: the budget is not in fact what limits delivery today. The native sampler
    // publishes ONE sample per SampleBufferQueue slot and the managed drain frees one slot per tick, so the
    // delivered rate is capped at one allocation sample per drain interval regardless of the budget -- this
    // run selected ~1600 samples and delivered 18, dropping the rest with "sample buffers full" (a native
    // Trace line). The assertions below deliberately do not depend on any per-payload sample count, only on
    // at least one drain producing a profile; see the task-9 report for the throughput finding.
    private const int MaxSamplesPerMinute = 6000;

    // How long the exerciser allocates for. At one delivered sample per drain interval this is many more
    // drains than the assertions need; the margin is for slower/loaded hosts, where the allocation rate (and
    // therefore the time to the first captured sample) is lower.
    private const int AllocateSeconds = 30;

    protected readonly TFixture _fixture;

    // The allocation sampler's own start line -- NOT the CPU path's "Session started; draining every ..." line,
    // which is emitted by the thread sampler's start and never fires on an allocation-only run.
    private static readonly string AllocationSamplingStartedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Allocation sampling started; up to (\d+) samples/minute\.";

    // Built-profile summary logged on every drain that produced something; group 2 is the byte count.
    // A drain that read no samples of either kind logs nothing at all, so a match here already means a
    // non-empty profile was built -- and on this allocation-only run, that it was built from allocation samples.
    private static readonly string BuiltProfileLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"\[ContinuousProfiling\] Posting profile \((\w+)\); (\d+) bytes to (\S+)\.";

    // Reused VERBATIM from ContinuousProfilingTests -- the payload dump is generic, not CPU-specific.
    // Group 1 captures the single-line protobuf-JSON blob in the same shape POSTed to the collector.
    private static readonly string ProfileJsonLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"Request\(.+?\): Invoked ""continuous_profiling"" with : (\{.*\})";

    // Also verbatim from ContinuousProfilingTests: allocation samples are interned into the SAME linkTable by
    // the same InternLink call, so the correlation evidence has exactly the same shape. The diagnostic log
    // rewrites the proto `bytes` id to lowercase hex, so the reserved "no link" entry is 32 zeros and any other
    // value proves a sample was correlated to a live transaction/span.
    private const string ZeroTraceIdHex = "00000000000000000000000000000000";
    private static readonly System.Text.RegularExpressions.Regex TraceIdInJsonRegex =
        new System.Text.RegularExpressions.Regex(@"""traceId"":""([0-9a-f]{32})""");

    // OTLP sample-type names for the two allocation profiles, interned as string-table entries and therefore
    // present verbatim in the dumped JSON (OtlpProfileBuilder: AllocatedObjectsSampleTypeName /
    // AllocatedSpaceSampleTypeName).
    private const string AllocatedObjectsSampleType = "allocated_objects";
    private const string AllocatedSpaceSampleType = "allocated_space";

    // The thread-sampling profiles' sample-type name. Must NOT appear: thread sampling is off, and its absence
    // is what makes the presence of the two names above attributable to the allocation path alone.
    private const string OffCpuSampleType = "off_cpu";

    private const string DrainMetricName = "Supportability/DotNET/ContinuousProfiling/Drain";
    private const string AllocationSamplesMetricName = "Supportability/DotNET/ContinuousProfiling/AllocationSamples";
    private const string ThreadSamplesMetricName = "Supportability/DotNET/ContinuousProfiling/Samples";

    protected ContinuousProfilingAllocationTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.SetTimeout(TimeSpan.FromMinutes(3));

        // Allocate synchronously and inline on a SINGLE thread, inside one instrumented [Transaction]/[Trace]
        // method. The allocation sampler reads the ALLOCATING thread's own trace context, and SetTraceContext is
        // pushed at the wrapper boundary keyed by the calling OS thread only (never propagated to spawned
        // threads), so keeping the allocation loop on the calling (traced) thread is what makes a captured
        // sample's trace/span link observable -- see ContinuousProfilingExerciser.RunCorrelatedAllocatingWork.
        _fixture.AddCommand($"ContinuousProfilingExerciser RunCorrelatedAllocatingWork {AllocateSeconds}");

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                // Debug (via finest) so the payload dump + correlation lines are emitted; faster metrics cycle
                // so the drain supportability metrics harvest within the test window (default cycle is 60s).
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);

                // Enable ALLOCATION sampling only, via the environment overrides (never ad-hoc config XML).
                // NEW_RELIC_CONTINUOUS_PROFILING_ENABLED is deliberately absent -- see the class remarks.
                _fixture.EnvironmentVariables["NEW_RELIC_CONTINUOUS_PROFILING_ALLOCATION_ENABLED"] = "true";
                _fixture.EnvironmentVariables["NEW_RELIC_CONTINUOUS_PROFILING_ALLOCATION_MAX_SAMPLES_PER_MINUTE"] = MaxSamplesPerMinute.ToString();
                // Drain cadence only, on this run: the thread sampler that would otherwise consume this as its
                // sampling interval is not started.
                _fixture.EnvironmentVariables["NEW_RELIC_CONTINUOUS_PROFILING_SAMPLING_INTERVAL_MS"] = DrainIntervalMs.ToString();
            },
            exerciseApplication: () =>
            {
                // The allocation sampler starts at agent init; confirm it before waiting on drain output.
                _fixture.AgentLog.WaitForLogLine(AllocationSamplingStartedLogLineRegex, TimeSpan.FromMinutes(1));

                // Wait for a drain to build a profile. On this run that cannot happen until allocation samples
                // have actually been captured and decoded -- a drain that reads nothing returns without logging.
                _fixture.AgentLog.WaitForLogLine(BuiltProfileLogLineRegex, TimeSpan.FromMinutes(2));

                // Best-effort wait for the Debug JSON payload line; the [Fact]s assert it directly.
                _fixture.AgentLog.TryGetLogLines(ProfileJsonLogLineRegex);

                // Give the metric harvest a chance to ship the drain supportability metrics.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    /// <summary>
    /// The dumped protobuf-JSON payloads, newest last, as raw single-line blobs.
    /// </summary>
    private string[] GetProfileJsonPayloads() =>
        _fixture.AgentLog.WaitForLogLines(ProfileJsonLogLineRegex, TimeSpan.FromSeconds(30))
            .Select(m => m.Groups[1].Value)
            .ToArray();

    [Fact]
    public void AllocationSamplingStartsWithConfiguredBudget()
    {
        var match = _fixture.AgentLog.WaitForLogLine(AllocationSamplingStartedLogLineRegex, TimeSpan.FromSeconds(30));
        var reportedBudget = int.Parse(match.Groups[1].Value);

        Assert.Equal(MaxSamplesPerMinute, reportedBudget);
    }

    [Fact]
    public void AllocationSamplingBuildsNonEmptyProfile()
    {
        // Multiple drains may occur; take the first that reports a non-empty ("built") profile. With thread
        // sampling off, a built profile can only have come from decoded allocation samples.
        var matches = _fixture.AgentLog.WaitForLogLines(BuiltProfileLogLineRegex, TimeSpan.FromSeconds(30)).ToArray();

        var builtProfile = matches.FirstOrDefault(m => m.Groups[1].Value == "built");

        NrAssert.Multiple(
            () => Assert.NotEmpty(matches),
            () => Assert.NotNull(builtProfile),
            () => Assert.True(int.Parse(builtProfile.Groups[2].Value) > 0, "Built profile reported zero bytes.")
        );
    }

    [Fact]
    public void AllocationProfilesAppearInBuiltProfileJson()
    {
        // Both allocation profiles are emitted together or not at all (they always carry the same sample count),
        // so a payload carrying one must carry the other. Substring checks against the raw blob, matching the
        // style of the CPU test's resourceProfiles/dictionary assertions -- the sample-type names are interned
        // string-table entries and appear verbatim in the dump.
        var payloads = GetProfileJsonPayloads();
        var allocationPayload = payloads.FirstOrDefault(p => p.Contains(AllocatedObjectsSampleType));

        NrAssert.Multiple(
            () => Assert.NotEmpty(payloads),
            () => Assert.NotNull(allocationPayload),
            () => Assert.Contains("resourceProfiles", allocationPayload),
            () => Assert.Contains("dictionary", allocationPayload),
            () => Assert.Contains(AllocatedObjectsSampleType, allocationPayload),
            () => Assert.Contains(AllocatedSpaceSampleType, allocationPayload),
            // Thread sampling never started on this run, so no cpu/off_cpu profile may be present. Its absence
            // is what makes the two names above attributable to the allocation path alone.
            () => Assert.DoesNotContain(OffCpuSampleType, allocationPayload)
        );
    }

    [Fact]
    public void AllocationProfileCarriesAllocatedTypeName()
    {
        // The allocated type name comes from the native AllocationTick payload parse (the field read from the
        // FRONT of the v4 payload, opposite AllocatedSize at the back), so seeing the exerciser's own allocated
        // type here is direct evidence that parse produced real data rather than an empty/garbage string. The
        // exerciser allocates byte arrays exclusively.
        //
        // Asserted across ALL payloads, not on the first one: the sampler is process-wide (it captures whatever
        // thread happens to allocate when a buffer slot is free), so an early drain can legitimately carry some
        // other type -- observed in practice as a System.Reflection.* allocation from the test app's own command
        // dispatch, before the exerciser's loop started. Requiring it in a SPECIFIC payload made this flaky.
        var payloads = GetProfileJsonPayloads();
        var allocationPayloads = payloads.Where(p => p.Contains(AllocatedObjectsSampleType)).ToArray();

        NrAssert.Multiple(
            () => Assert.NotEmpty(allocationPayloads),
            () => Assert.All(allocationPayloads, p => Assert.Contains("type.name", p)),
            () => Assert.Contains(allocationPayloads, p => p.Contains("System.Byte[]"))
        );
    }

    [Fact]
    public void AllocationSampleTakenDuringTransactionLogsNonZeroTraceSpanLink()
    {
        // The exerciser allocates synchronously, inline, on a SINGLE thread inside a [Transaction]/[Trace]-
        // instrumented method (RunCorrelatedAllocatingWork -> CorrelatedAllocatingTransaction ->
        // CorrelatedAllocate) for the whole run, without ever handing the work off to another thread. The
        // native tick handler reads the allocating thread's OWN trace-context slot, and that slot is only
        // populated for a thread currently executing inside an instrumented method, so keeping the allocation
        // loop on the calling (traced) thread is what makes the link observable. Across all drained payloads,
        // at least one linkTable entry must carry a non-zero trace id.
        var payloads = GetProfileJsonPayloads();

        var correlatedTraceIds = payloads
            .SelectMany(p => TraceIdInJsonRegex.Matches(p).Cast<System.Text.RegularExpressions.Match>())
            .Select(tid => tid.Groups[1].Value)
            .Where(tid => tid != ZeroTraceIdHex)
            .ToArray();

        NrAssert.Multiple(
            () => Assert.NotEmpty(payloads),
            () => Assert.NotEmpty(correlatedTraceIds)
        );
    }

    [Fact]
    public void AllocationDrainReportsSupportabilityMetrics()
    {
        // Both sample kinds funnel through the same drain/send path, so an allocation-only drain reports the
        // same Drain metric the CPU test asserts -- but the per-kind count is a SEPARATE metric
        // (.../AllocationSamples), reported only when allocation samples actually contributed. The thread-sample
        // count metric must be absent for the same reason off_cpu is: nothing thread-sampled on this run.
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var drainMetric = metrics.FirstOrDefault(x => x.MetricSpec.Name == DrainMetricName);
        var allocationSamplesMetric = metrics.FirstOrDefault(x => x.MetricSpec.Name == AllocationSamplesMetricName);
        var threadSamplesMetric = metrics.FirstOrDefault(x => x.MetricSpec.Name == ThreadSamplesMetricName);

        NrAssert.Multiple(
            () => Assert.NotNull(drainMetric),
            () => Assert.True(drainMetric.Values.CallCount > 0, "Drain metric call count was zero."),
            () => Assert.NotNull(allocationSamplesMetric),
            () => Assert.True(allocationSamplesMetric.Values.CallCount > 0, "AllocationSamples metric call count was zero."),
            () => Assert.Null(threadSamplesMetric)
        );
    }
}

public class ContinuousProfilingAllocationTestsCoreLatest : ContinuousProfilingAllocationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public ContinuousProfilingAllocationTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class ContinuousProfilingAllocationTestsCoreOldest : ContinuousProfilingAllocationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public ContinuousProfilingAllocationTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class ContinuousProfilingAllocationTestsCoreLatestX86 : ContinuousProfilingAllocationTestsBase<ConsoleDynamicMethodFixtureCoreLatestX86>
{
    public ContinuousProfilingAllocationTestsCoreLatestX86(ConsoleDynamicMethodFixtureCoreLatestX86 fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
