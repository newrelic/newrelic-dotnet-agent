// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;
using Telerik.JustMock;
using ExportProfilesRequest = OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

/// <summary>
/// Covers the allocation-sampling lifecycle, which is INDEPENDENT of the thread-sampling lifecycle covered by
/// <see cref="ContinuousProfilingServiceTests"/>: its own config flag, its own start/stop, but the same drain
/// tick, the same trace-context seam, the same send-failure backoff and the same one-request-per-drain payload.
///
/// Two contracts here are load-bearing enough to call out, because getting either wrong fails silently in the
/// field rather than in a test:
///   * The native allocation sampler's Shutdown is TERMINAL -- it latches and refuses every later Start -- so
///     every disable/pause/retune path must call Stop, and only Dispose may call Shutdown.
///   * The drain timer is shared, so it must stay armed while EITHER sampler is running and be released only
///     when both are stopped.
/// </summary>
[TestFixture]
public class ContinuousProfilingServiceAllocationTests
{
    private const int DefaultIntervalMs = 10000;
    private const int DefaultBudget = 200;

    private ISampleSource _source;
    private INativeContinuousProfiler _native;
    private IAllocationSampleSource _allocationSource;
    private IProfilesTransport _transport;
    private IScheduler _scheduler;
    private IAgentHealthReporter _health;
    private IConfiguration _config;
    private ContinuousProfilingService _service;

    [SetUp]
    public void SetUp()
    {
        _source = Mock.Create<ISampleSource>();
        _native = Mock.Create<INativeContinuousProfiler>();
        _allocationSource = Mock.Create<IAllocationSampleSource>();
        _transport = Mock.Create<IProfilesTransport>();
        _scheduler = Mock.Create<IScheduler>();
        _health = Mock.Create<IAgentHealthReporter>();
        _config = Mock.Create<IConfiguration>();

        // Default to "accepted" so a drain-oriented test doesn't accidentally trip the send-failure backoff.
        Mock.Arrange(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);

        _service = new ContinuousProfilingService(_source, _native, _allocationSource, _transport, _scheduler, _health);
        Connect();
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
        // Reset the process-wide seam so one test's enabled context can't leak into another.
        ContinuousProfilingContext.Instance = new ContinuousProfilingContext();
    }

    #region arrange helpers

    // Drains are gated on having connected (the profiles endpoint is only known post-preconnect), so every
    // service under test here is put into the connected state.
    private void Connect()
    {
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });
    }

    // A fresh (service, transport) pair, already connected. Needed whenever a test must control Send's result:
    // re-arranging the SAME shared mock in a test body does not take precedence over SetUp's arrangement (the
    // earlier-registered match wins), so a fresh transport mock is the only way to override it.
    private (ContinuousProfilingService Service, IProfilesTransport Transport) NewConnectedService()
    {
        var transport = Mock.Create<IProfilesTransport>();
        var service = new ContinuousProfilingService(_source, _native, _allocationSource, transport, _scheduler, _health);
        service.OverrideConfigForTesting(_config);
        Connect();
        return (service, transport);
    }

    // The config surface DrainOnce itself reads on its way to Send. Without these, the service falls back to
    // the real DefaultConfiguration.Instance and DrainOnce's outer catch silently swallows the failure -- every
    // drain test would then pass for the wrong reason (no send ever attempted).
    private IConfiguration ArrangeDrainableConfig(IConfiguration configuration)
    {
        Mock.Arrange(() => configuration.ApplicationNames).Returns(new[] { "MyApp" });
        Mock.Arrange(() => configuration.ContinuousProfilingIncludeAgentCode).Returns(false);
        return configuration;
    }

    // The three starting shapes, arranged on the fixture's shared _config and applied to the fixture's service.
    private void ArrangeAllocationOnly(int budget = DefaultBudget, int intervalMs = DefaultIntervalMs)
        => _service.OverrideConfigForTesting(ArrangeConfigOn(_config, threadSampling: false, allocation: true, intervalMs, budget));

    private void ArrangeThreadSamplingOnly(int intervalMs = DefaultIntervalMs)
        => _service.OverrideConfigForTesting(ArrangeConfigOn(_config, threadSampling: true, allocation: false, intervalMs, DefaultBudget));

    private void ArrangeBothEnabled(int budget = DefaultBudget, int intervalMs = DefaultIntervalMs)
        => _service.OverrideConfigForTesting(ArrangeConfigOn(_config, threadSampling: true, allocation: true, intervalMs, budget));

    // Returns a NEW configuration mock (never a re-arrangement of one already in use -- see NewConnectedService)
    // for tests that change configuration mid-test and then call ApplyConfigChange.
    private IConfiguration NewConfig(bool threadSampling, bool allocation, int intervalMs = DefaultIntervalMs, int budget = DefaultBudget)
        => ArrangeConfigOn(Mock.Create<IConfiguration>(), threadSampling, allocation, intervalMs, budget);

    private IConfiguration ArrangeConfigOn(IConfiguration configuration, bool threadSampling, bool allocation, int intervalMs, int budget)
    {
        Mock.Arrange(() => configuration.ContinuousProfilingEnabled).Returns(threadSampling);
        Mock.Arrange(() => configuration.ContinuousProfilingSamplingIntervalMs).Returns(intervalMs);
        Mock.Arrange(() => configuration.ContinuousProfilingAllocationEnabled).Returns(allocation);
        Mock.Arrange(() => configuration.ContinuousProfilingAllocationMaxSamplesPerMinute).Returns(budget);
        ArrangeDrainableConfig(configuration);
        return configuration;
    }

    private void ArrangeReadableThreadBatch()
    {
        var batch = OneThreadSampleBatch("worker-1", 1, 0, 0, 0, new[] { "F()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });
    }

    private void ArrangeReadableAllocationBatch()
    {
        var batch = OneAllocationSampleBatch();
        Mock.Arrange(() => _allocationSource.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });
    }

    #endregion

    #region start/stop gating -- each sampler on its own flag

    [Test]
    public void StartIfEnabled_with_allocation_enabled_starts_the_allocation_sampler_at_the_configured_budget()
    {
        ArrangeBothEnabled(budget: 350);

        _service.StartIfEnabled();

        Mock.Assert(() => _allocationSource.Start(350), Occurs.Once());
    }

    [Test]
    public void StartIfEnabled_with_allocation_disabled_does_not_start_the_allocation_sampler()
    {
        ArrangeThreadSamplingOnly();

        _service.StartIfEnabled();

        Mock.Assert(() => _allocationSource.Start(Arg.AnyInt), Occurs.Never());
        Mock.Assert(() => _native.Start(DefaultIntervalMs), Occurs.Once(), "the thread sampler must still start");
    }

    [Test]
    public void StartIfEnabled_with_allocation_enabled_and_thread_sampling_disabled_still_starts_allocation_sampling()
    {
        // The independence that matters: a disabled thread sampler must not short-circuit the allocation
        // sampler. Before this task, StartIfEnabled returned early on ContinuousProfilingEnabled == false.
        ArrangeAllocationOnly(budget: 200);

        _service.StartIfEnabled();

        Mock.Assert(() => _allocationSource.Start(200), Occurs.Once());
        Mock.Assert(() => _native.Start(Arg.AnyInt), Occurs.Never(), "the thread sampler is disabled and must stay stopped");
        Assert.That(_service.IsActive, Is.False, "IsActive reports the THREAD sampler; allocation sampling must not block thread profiling");
    }

    [Test]
    public void StartIfEnabled_while_the_allocation_sampler_is_already_active_does_not_restart_it()
    {
        ArrangeAllocationOnly();
        _service.StartIfEnabled();

        _service.StartIfEnabled();

        Mock.Assert(() => _allocationSource.Start(Arg.AnyInt), Occurs.Once());
    }

    [Test]
    public void StartIfEnabled_after_Dispose_starts_nothing()
    {
        ArrangeBothEnabled();

        _service.Dispose();
        _service.StartIfEnabled();

        Mock.Assert(() => _allocationSource.Start(Arg.AnyInt), Occurs.Never());
        Mock.Assert(() => _native.Start(Arg.AnyInt), Occurs.Never());
    }

    [Test]
    public void A_failed_allocation_start_unwinds_the_active_flag_so_a_later_start_can_retry()
    {
        // Only the FIRST start throws, so the second call proves the flag was actually unwound rather than left
        // true with nothing sampling -- which would also pin the shared drain timer open forever.
        var starts = 0;
        Mock.Arrange(() => _allocationSource.Start(Arg.AnyInt)).DoInstead(() =>
        {
            starts++;
            if (starts == 1)
                throw new InvalidOperationException("boom");
        });
        ArrangeAllocationOnly();

        Assert.DoesNotThrow(() => _service.StartIfEnabled());

        Mock.Assert(() => _allocationSource.Stop(), Occurs.Once(), "the failed start must unwind through Stop");
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Never(),
            "the drain schedule was never armed, so there is nothing to stop");

        _service.StartIfEnabled();

        Assert.That(starts, Is.EqualTo(2), "the second start must be allowed through, i.e. the active flag was unwound");
    }

    #endregion

    #region ApplyConfigChange

    [Test]
    public void ApplyConfigChange_enabling_allocation_from_disabled_starts_it()
    {
        ArrangeThreadSamplingOnly();
        _service.StartIfEnabled();

        _service.OverrideConfigForTesting(NewConfig(threadSampling: true, allocation: true, budget: 400));
        _service.ApplyConfigChange();

        Mock.Assert(() => _allocationSource.Start(400), Occurs.Once());
    }

    [Test]
    public void ApplyConfigChange_disabling_allocation_stops_it_and_never_shuts_it_down()
    {
        // The single most important assertion in this fixture: the native allocation sampler's Shutdown is a
        // terminal latch, so wiring a disable to it would permanently end allocation sampling for the life of
        // the process the first time anyone toggled the setting.
        ArrangeBothEnabled();
        _service.StartIfEnabled();

        _service.OverrideConfigForTesting(NewConfig(threadSampling: true, allocation: false));
        _service.ApplyConfigChange();

        Mock.Assert(() => _allocationSource.Stop(), Occurs.Once());
        Mock.Assert(() => _allocationSource.Shutdown(), Occurs.Never());
    }

    [Test]
    public void ApplyConfigChange_with_a_changed_budget_repaces_the_running_allocation_sampler_without_stopping_it()
    {
        ArrangeAllocationOnly(budget: 200);
        _service.StartIfEnabled();

        _service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: true, budget: 1000));
        _service.ApplyConfigChange();

        // Re-paced in place: the native Start is idempotent and resets the sub-sampler without reopening the
        // session, so there is no stop and no drain-timer churn.
        Mock.Assert(() => _allocationSource.Start(1000), Occurs.Once());
        Mock.Assert(() => _allocationSource.Stop(), Occurs.Never());
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Never());
    }

    [Test]
    public void ApplyConfigChange_with_an_unchanged_budget_does_not_restart_the_allocation_sampler()
    {
        ArrangeAllocationOnly(budget: 200);
        _service.StartIfEnabled();

        _service.ApplyConfigChange();

        Mock.Assert(() => _allocationSource.Start(Arg.AnyInt), Occurs.Once());
    }

    [Test]
    public void ApplyConfigChange_reconciles_allocation_even_while_the_cpu_bundle_is_command_owned()
    {
        // An agent command owns the cpu bundle until a matching stop, and ApplyConfigChange must not disturb
        // it. Command ownership is tracked PER TYPE, so a command that owns only the cpu bundle must leave
        // allocation sampling under config control -- the guard must not be an early return out of the whole
        // method.
        ArrangeThreadSamplingOnly();
        _service.StartFromCommand(new[] { "cpu" }, null, null);

        _service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: true, budget: 250));
        _service.ApplyConfigChange();

        Mock.Assert(() => _allocationSource.Start(250), Occurs.Once(), "allocation config must be applied despite the cpu bundle being command-owned");
        Mock.Assert(() => _native.Stop(), Occurs.Never(), "the command-owned thread sampler must be left alone");
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void ApplyConfigChange_after_Dispose_does_not_resurrect_allocation_sampling()
    {
        ArrangeAllocationOnly();
        _service.Dispose();

        _service.ApplyConfigChange();

        Mock.Assert(() => _allocationSource.Start(Arg.AnyInt), Occurs.Never());
    }

    #endregion

    #region the heap agent-command token

    [Test]
    public void StartFromCommand_with_heap_starts_allocation_sampling_even_while_config_has_it_disabled()
    {
        // The point of an operator command: it starts a sampler the configuration does not enable. The BUDGET
        // still comes from configuration, because the command carries none of its own (its two interval
        // parameters are both cpu-shaped).
        _service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: false, budget: 275));

        var result = _service.StartFromCommand(new[] { "heap" }, null, null);

        Mock.Assert(() => _allocationSource.Start(275), Occurs.Once());
        Mock.Assert(() => _native.Start(Arg.AnyInt), Occurs.Never(), "\"heap\" is allocations only");
        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveTypes, Is.EqualTo(new[] { "heap" }));
            Assert.That(result.Exceptions, Is.Empty, "allocation sampling is implemented now, so nothing is unsupported");
        });
    }

    [Test]
    public void StartFromCommand_with_heap_arms_the_shared_drain_schedule_and_the_trace_context_seam()
    {
        // The command path must go through the same StartAllocationLocked as the config path, not a shortcut
        // that starts the native sampler with nothing draining it.
        ArrangeAllocationOnly(intervalMs: 15000);

        _service.StartFromCommand(new[] { "heap" }, null, null);

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(15000), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.True);
    }

    [Test]
    public void StartFromCommand_with_heap_while_allocation_is_already_active_is_an_idempotent_noop()
    {
        ArrangeAllocationOnly(budget: 200);
        _service.StartIfEnabled();

        var result = _service.StartFromCommand(new[] { "heap" }, null, null);

        Mock.Assert(() => _allocationSource.Start(Arg.AnyInt), Occurs.Once(), "a repeat start must not re-pace a running sampler");
        Assert.That(result.ActiveTypes, Is.EqualTo(new[] { "heap" }));
    }

    [Test]
    public void StopFromCommand_with_heap_stops_allocation_sampling_and_releases_the_shared_drain()
    {
        ArrangeAllocationOnly();
        _service.StartFromCommand(new[] { "heap" }, null, null);

        var result = _service.StopFromCommand(new[] { "heap" });

        Mock.Assert(() => _allocationSource.Stop(), Occurs.Once());
        Mock.Assert(() => _allocationSource.Shutdown(), Occurs.Never(), "a command stop must never be the terminal shutdown");
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Once(),
            "nothing else is sampling, so the shared drain is released");
        Assert.That(result.ActiveTypes, Is.Empty);
    }

    [Test]
    public void StopFromCommand_with_heap_while_allocation_is_inactive_is_an_idempotent_noop()
    {
        ArrangeAllocationOnly();

        var result = _service.StopFromCommand(new[] { "heap" });

        Mock.Assert(() => _allocationSource.Stop(), Occurs.Never());
        Assert.That(result.Exceptions, Is.Empty);
    }

    [Test]
    public void A_command_started_allocation_sampler_is_immune_to_an_unrelated_config_update_until_explicitly_stopped()
    {
        // The heap mirror of the cpu ownership test in ContinuousProfilingServiceTests: an incidental
        // config-update event must not silently undo an operator's explicit command.
        ArrangeAllocationOnly();
        _service.StartFromCommand(new[] { "heap" }, null, null);

        _service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: false));
        _service.ApplyConfigChange();

        Mock.Assert(() => _allocationSource.Stop(), Occurs.Never());

        // An explicit stop command releases ownership; a subsequent config update can act again.
        _service.StopFromCommand(new[] { "heap" });
        _service.ApplyConfigChange(); // config still says disabled -- already stopped, so no second stop

        Mock.Assert(() => _allocationSource.Stop(), Occurs.Once());
    }

    [Test]
    public void ApplyConfigChange_does_not_repace_a_command_owned_allocation_sampler()
    {
        // Ownership covers the budget too, not just start/stop: a server-side budget push must not silently
        // re-pace a sampler an operator started by command.
        ArrangeAllocationOnly(budget: 200);
        _service.StartFromCommand(new[] { "heap" }, null, null);

        _service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: true, budget: 900));
        _service.ApplyConfigChange();

        Mock.Assert(() => _allocationSource.Start(900), Occurs.Never());
        Mock.Assert(() => _allocationSource.Start(200), Occurs.Once());
    }

    [Test]
    public void ApplyConfigChange_still_reconciles_thread_sampling_while_allocation_is_command_owned()
    {
        // The other direction of the per-type guard: heap ownership must not freeze the thread sampler's
        // config-driven reconciliation.
        ArrangeAllocationOnly();
        _service.StartFromCommand(new[] { "heap" }, null, null);

        // Note this config also DISABLES allocation, which is exactly what the heap ownership guard has to
        // ignore.
        _service.OverrideConfigForTesting(NewConfig(threadSampling: true, allocation: false, intervalMs: 20000));
        _service.ApplyConfigChange();

        Mock.Assert(() => _native.Start(20000), Occurs.Once(), "the thread sampler is still config-controlled");
        Mock.Assert(() => _allocationSource.Stop(), Occurs.Never(), "the command-owned allocation sampler must be left alone");
        Assert.That(_service.IsActive, Is.True);

        // And allocation sampling is genuinely still live, not merely un-stopped: the drain still reads it,
        // which it only does while the allocation active flag is set.
        ArrangeReadableAllocationBatch();
        _service.DrainOnce();
        Mock.Assert(() => _allocationSource.ReadBatch(Arg.IsAny<byte[]>()), Occurs.Once());
    }

    [Test]
    public void An_empty_include_command_reports_a_config_started_allocation_sampler_as_active()
    {
        // The status-query shape of the command (empty include changes nothing): ActiveTypes must report the
        // allocation sampler even though configuration, not a command, started it.
        ArrangeAllocationOnly();
        _service.StartIfEnabled();

        var result = _service.StartFromCommand(Array.Empty<string>(), null, null);

        Mock.Assert(() => _allocationSource.Start(Arg.AnyInt), Occurs.Once(), "an empty include must not start anything");
        Assert.That(result.ActiveTypes, Is.EqualTo(new[] { "heap" }));
    }

    [Test]
    public void A_command_result_reports_both_samplers_when_both_are_active()
    {
        ArrangeBothEnabled();
        _service.StartIfEnabled();

        var result = _service.StopFromCommand(Array.Empty<string>());

        Mock.Assert(() => _native.Stop(), Occurs.Never(), "an empty include must not stop anything");
        Mock.Assert(() => _allocationSource.Stop(), Occurs.Never());
        Assert.That(result.ActiveTypes, Is.EqualTo(new[] { "cpu", "heap" }));
    }

    #endregion

    #region the shared drain schedule and trace-context seam

    [Test]
    public void Allocation_only_arms_the_drain_schedule_at_the_configured_sampling_interval()
    {
        // The drain timer used to be armed exclusively by the thread-sampling start, which would have left an
        // allocation-only session filling native buffers that nothing ever drained.
        ArrangeAllocationOnly(intervalMs: 15000);

        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(15000), Arg.IsAny<TimeSpan?>()), Occurs.Once());
    }

    [Test]
    public void Allocation_only_arms_the_trace_context_seam()
    {
        // Allocation samples correlate through the thread sampler's native trace-context map, and the native
        // setter does not require the thread sampler's session -- so an allocation-only session must still arm
        // the push seam or every allocation sample would lose its trace/span link.
        ArrangeAllocationOnly();

        _service.StartIfEnabled();

        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.True);
    }

    [Test]
    public void Both_samplers_enabled_arm_the_drain_schedule_once_at_the_thread_sampling_interval()
    {
        ArrangeBothEnabled(intervalMs: 12000);

        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(12000), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()), Occurs.Once(),
            "the two samplers share one drain timer; the second start must not register another");
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Never(),
            "and must not retune the one already armed");
    }

    [Test]
    public void Stopping_thread_sampling_while_allocation_is_active_keeps_the_drain_schedule_and_seam_armed()
    {
        ArrangeBothEnabled();
        _service.StartIfEnabled();

        _service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: true));
        _service.ApplyConfigChange();

        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Never(),
            "the allocation sampler still needs the shared drain");
        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.True, "allocation samples still need trace-context correlation");
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void Stopping_allocation_while_thread_sampling_is_active_keeps_the_drain_schedule_armed()
    {
        ArrangeBothEnabled();
        _service.StartIfEnabled();

        _service.OverrideConfigForTesting(NewConfig(threadSampling: true, allocation: false));
        _service.ApplyConfigChange();

        Mock.Assert(() => _allocationSource.Stop(), Occurs.Once());
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Never());
        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.True);
    }

    [Test]
    public void Stopping_both_samplers_releases_the_drain_schedule_and_the_seam()
    {
        ArrangeBothEnabled();
        _service.StartIfEnabled();

        _service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: false));
        _service.ApplyConfigChange();

        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.False);
    }

    [Test]
    public void StopFromCommand_for_cpu_while_allocation_is_active_leaves_allocation_sampling_and_the_drain_running()
    {
        // The realistic route to an allocation-only session: both configured on, then an operator stops the cpu
        // bundle by command.
        ArrangeBothEnabled();
        _service.StartIfEnabled();

        _service.StopFromCommand(new[] { "cpu" });

        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Mock.Assert(() => _allocationSource.Stop(), Occurs.Never());
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Never());

        // And the drain still ships allocation-only payloads.
        ArrangeReadableAllocationBatch();
        _service.DrainOnce();
        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Once());
    }

    [Test]
    public void Re_enabling_thread_sampling_at_a_new_interval_retunes_the_shared_drain()
    {
        ArrangeAllocationOnly(intervalMs: 10000);
        _service.StartIfEnabled();

        _service.OverrideConfigForTesting(NewConfig(threadSampling: true, allocation: true, intervalMs: 30000));
        _service.ApplyConfigChange();

        // The thread sampler's interval is the authoritative cadence, so it retunes the timer the allocation
        // path armed at the config interval.
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(30000), Arg.IsAny<TimeSpan?>()), Occurs.Once());
    }

    #endregion

    #region DrainOnce

    [Test]
    public void Drain_tick_with_only_allocation_samples_builds_and_sends()
    {
        // The widened early-return: a sweep with zero thread samples but non-zero allocation samples must
        // still be built and shipped.
        ArrangeAllocationOnly();
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();

        ExportProfilesRequest captured = null;
        Mock.Arrange(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns((ExportProfilesRequest r) =>
        {
            captured = r;
            return true;
        });

        _service.DrainOnce();

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Once());
        Assert.That(captured, Is.Not.Null);
        // allocated_objects + allocated_space, and no cpu/off_cpu profile (there were no thread samples, and
        // the thread sampler's interval -- the profile period -- is zero while it is stopped).
        Assert.That(captured.ResourceProfiles[0].ScopeProfiles[0].Profiles, Has.Count.EqualTo(2));
    }

    [Test]
    public void Drain_tick_with_both_sample_types_ships_them_in_one_request()
    {
        ArrangeBothEnabled();
        _service.StartIfEnabled();
        ArrangeReadableThreadBatch();
        ArrangeReadableAllocationBatch();

        ExportProfilesRequest captured = null;
        Mock.Arrange(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns((ExportProfilesRequest r) =>
        {
            captured = r;
            return true;
        });

        _service.DrainOnce();

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Once(),
            "both sample types must ride ONE request, not one request each");
        // off_cpu (the thread batch is version 1, so no sample is classified on-CPU) + allocated_objects +
        // allocated_space.
        Assert.That(captured.ResourceProfiles[0].ScopeProfiles[0].Profiles, Has.Count.EqualTo(3));
    }

    [Test]
    public void Drain_tick_with_neither_sample_type_does_not_send()
    {
        ArrangeAllocationOnly();
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        Mock.Arrange(() => _allocationSource.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);

        _service.DrainOnce();

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
    }

    [Test]
    public void Drain_tick_does_not_read_the_allocation_source_while_allocation_sampling_is_inactive()
    {
        // The thread sampler arms the shared drain on its own, so an ungated allocation read would P/Invoke a
        // never-started sampler on every tick for the life of the process.
        ArrangeThreadSamplingOnly();
        _service.StartIfEnabled();
        ArrangeReadableThreadBatch();

        _service.DrainOnce();

        Mock.Assert(() => _allocationSource.ReadBatch(Arg.IsAny<byte[]>()), Occurs.Never());
        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Once());
    }

    [Test]
    public void Drain_tick_reports_the_allocation_samples_supportability_metric()
    {
        ArrangeAllocationOnly();
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();

        _service.DrainOnce();

        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Drain"), Occurs.Once());
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/AllocationSamples", 1), Occurs.Once());
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Samples", Arg.IsAny<long>()), Occurs.Never(),
            "no thread samples in this sweep -- reporting a zero count would be noise");
    }

    [Test]
    public void Drain_tick_never_throws_when_the_allocation_source_throws()
    {
        ArrangeAllocationOnly();
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        Mock.Arrange(() => _allocationSource.ReadBatch(Arg.IsAny<byte[]>())).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _service.DrainOnce());
        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
    }

    [Test]
    public void Drain_tick_discards_an_allocation_batch_that_overruns_the_drain_buffer()
    {
        ArrangeAllocationOnly();
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        Mock.Arrange(() => _allocationSource.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) => dest.Length + 1);

        Assert.DoesNotThrow(() => _service.DrainOnce());

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Error"), Occurs.Once());
    }

    [Test]
    public void Drain_tick_reports_the_boundary_metric_for_an_allocation_batch_that_fills_the_drain_buffer()
    {
        ArrangeAllocationOnly();
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        Mock.Arrange(() => _allocationSource.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) => dest.Length);

        _service.DrainOnce();

        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/DrainBufferBoundary"), Occurs.Once());
    }

    [Test]
    public void Drain_tick_after_Dispose_reads_neither_source()
    {
        ArrangeAllocationOnly();
        _service.StartIfEnabled();
        ArrangeReadableAllocationBatch();

        _service.Dispose();
        _service.DrainOnce();

        Mock.Assert(() => _allocationSource.ReadBatch(Arg.IsAny<byte[]>()), Occurs.Never());
        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
    }

    #endregion

    #region Dispose -- the one and only Shutdown

    [Test]
    public void Dispose_shuts_down_the_allocation_sampler_exactly_once()
    {
        ArrangeBothEnabled();
        _service.StartIfEnabled();

        _service.Dispose();

        Mock.Assert(() => _allocationSource.Shutdown(), Occurs.Once());
    }

    [Test]
    public void Dispose_shuts_down_the_allocation_sampler_even_when_it_was_never_started()
    {
        // The native session is closed and its in-flight work drained deterministically on teardown, exactly as
        // for the thread profiler, whether or not this process ever sampled.
        _service.Dispose();

        Mock.Assert(() => _allocationSource.Shutdown(), Occurs.Once());
    }

    [Test]
    public void Dispose_stops_an_active_allocation_sampler_before_shutting_it_down()
    {
        ArrangeAllocationOnly();
        _service.StartIfEnabled();

        _service.Dispose();

        Mock.Assert(() => _allocationSource.Stop(), Occurs.Once());
        Mock.Assert(() => _allocationSource.Shutdown(), Occurs.Once());
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Once(),
            "the drain armed by the allocation-only session must be released on teardown");
    }

    [Test]
    public void Dispose_does_not_throw_when_the_allocation_shutdown_throws()
    {
        Mock.Arrange(() => _allocationSource.Shutdown()).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _service.Dispose());
    }

    [Test]
    public void Dispose_still_shuts_down_the_allocation_sampler_when_the_thread_profiler_shutdown_throws()
    {
        // Separately try/caught: one native teardown failing must not skip the other, or an EventPipe session
        // would be left open for the life of the process.
        Mock.Arrange(() => _native.Shutdown()).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _service.Dispose());
        Mock.Assert(() => _allocationSource.Shutdown(), Occurs.Once());
    }

    #endregion

    #region send-failure backoff

    [Test]
    public void Tripping_backoff_pauses_allocation_sampling()
    {
        // Both sample types ride the request that just failed, so there is nothing to gain from continuing to
        // pay for allocation stack walks on customer threads while the drain is gated off.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeAllocationOnly();
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // two consecutive failures trip the backoff

        Mock.Assert(() => _allocationSource.Stop(), Occurs.Once());
        Mock.Assert(() => _allocationSource.Shutdown(), Occurs.Never(), "a pause must never be a terminal shutdown");
    }

    [Test]
    public void A_backoff_probe_resumes_an_allocation_only_session_at_the_same_budget()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeAllocationOnly(budget: 750);
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();

        Action probe = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probe = action);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips, schedules the probe

        probe.Invoke();

        // Once from the start, once from the probe's resume -- and the thread sampler, which was never
        // enabled, must not be resumed by an allocation-only probe.
        Mock.Assert(() => _allocationSource.Start(750), Occurs.Exactly(2));
        Mock.Assert(() => _native.Start(Arg.AnyInt), Occurs.Never());

        // The gate is clear again, so the next drain reaches Send.
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();
        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(3));
    }

    [Test]
    public void Starting_thread_sampling_during_a_backoff_pause_also_resumes_the_paused_allocation_sampler()
    {
        // Regression test for a silent, PERMANENT failure: the trip pauses both natives but leaves both active
        // flags true, and the pending probe is the only thing that ever resumes them. A thread-sampler start
        // clears the backoff gate and bumps the generation, superseding that probe -- so unless it resumes
        // allocation itself, allocation stays Stop()'d forever with _allocationActive still reading true, and
        // nothing ever tries again.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeAllocationOnly(budget: 300);
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff: allocation native Stop()'d, flag still true, probe pending
        Mock.Assert(() => _allocationSource.Stop(), Occurs.Once());

        // The thread sampler starts later (by command here; a config enable takes the same path).
        service.StartFromCommand(new[] { "cpu" }, null, null);

        // Once from the original start, once from this start's resume-the-other-sampler step.
        Mock.Assert(() => _allocationSource.Start(300), Occurs.Exactly(2),
            "the start that superseded the probe owes the paused allocation sampler a resume");
    }

    [Test]
    public void Starting_allocation_during_a_backoff_pause_also_resumes_the_paused_thread_sampler()
    {
        // The mirror of the above: an allocation start clears the same shared gate and supersedes the same
        // probe, so it owes the paused thread sampler a resume.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeThreadSamplingOnly(intervalMs: 11000);
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        ArrangeReadableThreadBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff: thread native Stop()'d, _isActive still true
        Mock.Assert(() => _native.Stop(), Occurs.Once());

        service.OverrideConfigForTesting(NewConfig(threadSampling: true, allocation: true, intervalMs: 11000));
        service.ApplyConfigChange();

        Mock.Assert(() => _native.Start(11000), Occurs.Exactly(2),
            "the allocation start that superseded the probe owes the paused thread sampler a resume");
    }

    [Test]
    public void Starting_thread_sampling_resumes_the_paused_allocation_sampler_even_when_its_own_native_start_throws()
    {
        // Regression test for the exception path of the resume fix. The start clears the gate and supersedes the
        // probe BEFORE doing anything else, so the debt owed to the other sampler must be paid before this
        // sampler takes its own risk. If the resume sat after the throwing own-start, the catch (which only
        // unwinds THIS sampler) would leave allocation stopped with its flag true and no probe left to fire --
        // permanently stranded.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeAllocationOnly(budget: 300);
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff: allocation paused, flag still true, probe pending

        // The thread sampler's own native start fails.
        Mock.Arrange(() => _native.Start(Arg.AnyInt)).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => service.StartFromCommand(new[] { "cpu" }, null, null));

        Mock.Assert(() => _allocationSource.Start(300), Occurs.Exactly(2),
            "allocation must be resumed even though the thread sampler's own start threw");
        Assert.That(service.IsActive, Is.False, "the failed thread start still unwinds itself");
    }

    [Test]
    public void Starting_allocation_resumes_the_paused_thread_sampler_even_when_its_own_native_start_throws()
    {
        // The mirror direction of the above.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeThreadSamplingOnly(intervalMs: 11000);
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        ArrangeReadableThreadBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff: thread sampler paused, _isActive still true, probe pending

        // The allocation sampler's own native start fails.
        Mock.Arrange(() => _allocationSource.Start(Arg.AnyInt)).Throws(new InvalidOperationException("boom"));

        service.OverrideConfigForTesting(NewConfig(threadSampling: true, allocation: true, intervalMs: 11000));
        Assert.DoesNotThrow(() => service.ApplyConfigChange());

        Mock.Assert(() => _native.Start(11000), Occurs.Exactly(2),
            "thread sampling must be resumed even though the allocation sampler's own start threw");
    }

    [Test]
    public void Resuming_after_backoff_resets_the_drain_timestamp_so_the_next_profile_is_not_stretched_over_the_pause()
    {
        // Each resume helper owns its own _lastDrainTimestamp reset; without it the first profile after the
        // resume reports a duration spanning the whole paused window (up to the 300s backoff cap) rather than
        // one real interval. This exercises the thread-sampling resume specifically, because in this scenario
        // the drain timer is already armed -- so StartAllocationLocked's trailing ArmDrainScheduleLocked (which
        // also resets the timestamp) is NOT reached, and only the helper's own reset can save it.
        const int PauseMs = 300;

        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeThreadSamplingOnly();
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        ArrangeReadableThreadBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff; _lastDrainTimestamp is left at this moment

        // Stand in for waiting out a real backoff delay.
        Thread.Sleep(PauseMs);

        // Enabling allocation resumes the paused thread sampler.
        service.OverrideConfigForTesting(NewConfig(threadSampling: true, allocation: true));
        service.ApplyConfigChange();

        // Re-arranging this locally-created mock works (the existing backoff tests flip false -> true the same
        // way); only SetUp's arrangement on the shared _transport can't be overridden in a test body.
        ExportProfilesRequest captured = null;
        Mock.Arrange(() => _allocationSource.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns((ExportProfilesRequest r) =>
        {
            captured = r;
            return true;
        });

        service.DrainOnce();

        Assert.That(captured, Is.Not.Null);
        var durationNano = captured.ResourceProfiles[0].ScopeProfiles[0].Profiles[0].DurationNano;

        // Generous threshold: with the reset this is the few ms of mock/build work since the resume; without it
        // this would be at least the full PauseMs. Half the pause leaves a wide margin against CI jitter.
        Assert.That(durationNano, Is.LessThan((ulong)PauseMs / 2 * 1_000_000UL),
            "the first profile after a resume must not be stretched over the backoff pause");
    }

    [Test]
    public void A_failure_to_resume_allocation_after_backoff_does_not_unwind_the_thread_sampler_start()
    {
        // Isolation, the other direction: the resume helper owns its errors, so a broken allocation sampler
        // fails only allocation sampling. The thread-sampler start that triggered the resume must still succeed.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeAllocationOnly();
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff

        // Only the RESUME attempt throws; the original start above already succeeded.
        Mock.Arrange(() => _allocationSource.Start(Arg.AnyInt)).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => service.StartFromCommand(new[] { "cpu" }, null, null));

        Assert.That(service.IsActive, Is.True, "the thread sampler start must not be unwound by a failed allocation resume");
        // Once from the backoff trip's pause, once from the failed resume unwinding allocation (fail closed).
        Mock.Assert(() => _allocationSource.Stop(), Occurs.Exactly(2));
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Never(),
            "the thread sampler is running, so the shared drain must stay armed");
    }

    [Test]
    public void A_failure_to_resume_thread_sampling_after_backoff_does_not_unwind_the_allocation_start()
    {
        // And the mirror: a broken thread sampler must not take the allocation start down with it.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeThreadSamplingOnly();
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        ArrangeReadableThreadBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff

        // Only the RESUME attempt throws; the original start above already succeeded.
        Mock.Arrange(() => _native.Start(Arg.AnyInt)).Throws(new InvalidOperationException("boom"));

        service.OverrideConfigForTesting(NewConfig(threadSampling: true, allocation: true, budget: 275));
        Assert.DoesNotThrow(() => service.ApplyConfigChange());

        Mock.Assert(() => _allocationSource.Start(275), Occurs.Once(),
            "the allocation start must not be unwound by a failed thread-sampling resume");
        Assert.That(service.IsActive, Is.False, "the thread sampler failed to resume and is stopped");

        // And allocation is genuinely live: it re-armed the shared drain and a drain ships its samples.
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();
        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(3));
    }

    [Test]
    public void Disabling_and_re_enabling_allocation_while_backing_off_clears_the_stuck_gate()
    {
        // Same stuck-gate hazard the thread-sampling path was already fixed for: a probe that fires while
        // nothing is active cannot clear _sendBackoffActive, so without the allocation start performing the
        // optimistic reset, re-enabling allocation would sample forever with every drain silently gated off.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeAllocationOnly();
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();

        Action probe = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probe = action);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff, schedules a probe

        service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: false));
        service.ApplyConfigChange();

        probe.Invoke(); // fires while nothing is active -- cannot clear the gate

        service.OverrideConfigForTesting(_config);
        service.ApplyConfigChange(); // re-enable allocation

        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();

        // 2 failures before the disable + 1 recovery send after re-enabling. Without the reset this third send
        // would never happen, because the drain would still be gated.
        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(3));
    }

    [Test]
    public void A_budget_change_during_a_backoff_pause_does_not_re_arm_sampling_or_collapse_the_round()
    {
        // A budget edit is no evidence the send path recovered, so it must neither clear the gate nor re-arm the
        // native -- re-arming would buy real stack walks on customer threads whose output the gated drain
        // discards. The new budget is recorded and takes effect when the probe resumes.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeAllocationOnly(budget: 200);
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();

        Action probe = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probe = action);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff

        service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: true, budget: 900));
        service.ApplyConfigChange();

        Mock.Assert(() => _allocationSource.Start(900), Occurs.Never(), "sampling must stay paused while backing off");

        // The probe resumes at the NEW budget, so the edit was recorded rather than dropped.
        probe.Invoke();
        Mock.Assert(() => _allocationSource.Start(900), Occurs.Once());
    }

    [Test]
    public void A_probe_firing_after_allocation_was_disabled_does_not_resume_it()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        ArrangeAllocationOnly();
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);
        ArrangeReadableAllocationBatch();

        Action probe = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probe = action);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff, schedules a probe

        service.OverrideConfigForTesting(NewConfig(threadSampling: false, allocation: false));
        service.ApplyConfigChange();

        probe.Invoke();

        // Only the original start: reviving a sampler the configuration no longer wants would be wrong.
        Mock.Assert(() => _allocationSource.Start(Arg.AnyInt), Occurs.Once());
    }

    #endregion

    #region batch builders (mirror BufferParserTests / BufferParserAllocationTests)

    private const byte StartBatch = 0x01, StartSample = 0x02, EndBatch = 0x06, AllocationSample = 0x08;

    private static void WriteShort(MemoryStream s, short v) { s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    private static void WriteLong(MemoryStream s, long v) { for (var i = 7; i >= 0; i--) s.WriteByte((byte)(v >> (i * 8))); }
    private static void WriteString(MemoryStream s, string v)
    {
        var bytes = Encoding.Unicode.GetBytes(v); // UTF-16LE
        WriteShort(s, (short)v.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static byte[] OneThreadSampleBatch(string thread, long osId, long tHigh, long tLow, long span, string[] framesLeafFirst)
    {
        using var s = new MemoryStream();
        s.WriteByte(StartBatch); s.WriteByte(1); WriteLong(s, 123456789L); // version + timestamp
        s.WriteByte(StartSample);
        WriteString(s, thread); WriteLong(s, osId); WriteLong(s, tHigh); WriteLong(s, tLow); WriteLong(s, span);
        short next = 1;
        foreach (var f in framesLeafFirst) { WriteShort(s, (short)-next); WriteString(s, f); next++; }
        WriteShort(s, 0); // end of frames
        s.WriteByte(EndBatch);
        return s.ToArray();
    }

    // One 0x08 allocation-sample record, in the field order the native SampleBufferWriter emits.
    private static byte[] OneAllocationSampleBatch()
    {
        using var s = new MemoryStream();
        s.WriteByte(StartBatch); s.WriteByte(2); WriteLong(s, 123456789L); // version + timestamp
        s.WriteByte(AllocationSample);
        WriteString(s, "worker-1");
        WriteLong(s, 4242L);                  // OS thread id
        WriteLong(s, 0x11); WriteLong(s, 0x22); WriteLong(s, 0x33); // traceIdHigh/Low, spanId
        WriteLong(s, 1700000000000L);         // timestampMillis
        WriteLong(s, 65536L);                 // allocatedSize
        WriteString(s, "MyApp.Widget");       // type name
        WriteShort(s, -1); WriteString(s, "MyApp.Widget.Create()"); // frame, first sight -> define
        WriteShort(s, 0); // end of frames
        s.WriteByte(EndBatch);
        return s.ToArray();
    }

    #endregion
}
