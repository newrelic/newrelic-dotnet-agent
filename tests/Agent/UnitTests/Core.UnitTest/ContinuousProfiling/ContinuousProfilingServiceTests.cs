// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DataTransport.ContinuousProfiling;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;
using Telerik.JustMock;
using ExportProfilesRequest = OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ContinuousProfilingServiceTests
{
    private ISampleSource _source;
    private INativeContinuousProfiler _native;
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
        _transport = Mock.Create<IProfilesTransport>();
        _scheduler = Mock.Create<IScheduler>();
        _health = Mock.Create<IAgentHealthReporter>();
        _config = Mock.Create<IConfiguration>();

        // Send now returns bool; default to "accepted" so every existing Drain_tick_* test below keeps
        // exercising healthy-send behavior. Otherwise an unarranged mock returns false, which would trip
        // the send-failure backoff after two drains and pause native sampling mid-test.
        Mock.Arrange(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);

        _service = new ContinuousProfilingService(_source, _native, _transport, _scheduler, _health);

        // DrainOnce is gated on having connected -- put _service into the connected state so every
        // existing test below (none of which care about the pre-connect gate) keeps exercising
        // "already connected" behavior. The pre-connect gate itself is tested separately, against a
        // service instance that deliberately never receives this event.
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
        // Reset the process-wide seam and the hot-path pre-filter so one test's enabled context can't leak
        // into another (Enable/Disable on the real service flips AnyEnabled).
        ContinuousProfilingContext.Instance = new ContinuousProfilingContext();
        ContinuousProfilingContext.AnyEnabled = false;
    }

    private void ArrangeEnabled(int intervalMs = 10000, bool highSecurityModeEnabled = false)
    {
        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _config.ContinuousProfilingSamplingIntervalMs).Returns(intervalMs);
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        Mock.Arrange(() => _config.HighSecurityModeEnabled).Returns(highSecurityModeEnabled);
        _service.OverrideConfigForTesting(_config);
    }

    [Test]
    public void Enabling_via_config_starts_the_drain_schedule()
    {
        ArrangeEnabled(10000);

        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(10000), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void The_drain_action_registered_with_the_scheduler_dispatches_DrainOnce_asynchronously_instead_of_running_it_inline()
    {
        // ArrangeEnabled: StartIfEnabled is a no-op (and never registers a drain action) against an
        // unarranged config mock, whose ContinuousProfilingEnabled defaults to false -- not called out in
        // the brief's literal test body, added here so the test actually reaches ExecuteEvery.
        ArrangeEnabled(10000);

        Action drainAction = null;
        Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()))
            .DoInstead((Action action, TimeSpan interval, TimeSpan? initialDelay, bool trackAsAgentWork) => drainAction = action);

        _service.StartIfEnabled();

        Assert.That(drainAction, Is.Not.Null);

        // Calling the registered action must return promptly even though DrainOnce itself would block on
        // _sampleSource.ReadBatch/_transport.Send -- proving it's dispatched, not invoked inline. Arrange a
        // slow ReadBatch to make an inline call observably hang if this regresses.
        var readBatchStarted = new ManualResetEventSlim(false);
        var releaseReadBatch = new ManualResetEventSlim(false);
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>()))
            .DoInstead(() => { readBatchStarted.Set(); releaseReadBatch.Wait(TimeSpan.FromSeconds(5)); })
            .Returns(0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        drainAction();
        sw.Stop();

        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)), "the registered action must dispatch, not block, the calling thread");
        Assert.That(readBatchStarted.Wait(TimeSpan.FromSeconds(5)), Is.True, "the dispatched drain must actually run on some thread");

        releaseReadBatch.Set();
    }

    [Test]
    public void StartIfEnabled_when_disabled_does_not_schedule()
    {
        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(_config);

        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Never());
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void StartIfEnabled_when_already_active_does_not_reschedule()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Once());
    }

    [Test]
    public void Disabling_via_config_stops_the_drain_schedule()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var disabled = Mock.Create<IConfiguration>();
        Mock.Arrange(() => disabled.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(disabled);
        _service.ApplyConfigChange();

        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void ApplyConfigChange_still_enabled_same_interval_does_not_restart()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        _service.ApplyConfigChange();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Once());
        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Never());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void ApplyConfigChange_interval_change_while_running_retunes()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var retuned = Mock.Create<IConfiguration>();
        Mock.Arrange(() => retuned.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => retuned.ContinuousProfilingSamplingIntervalMs).Returns(20000);
        Mock.Arrange(() => retuned.ApplicationNames).Returns(new[] { "MyApp" });
        _service.OverrideConfigForTesting(retuned);
        _service.ApplyConfigChange();

        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(20000), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void StartFromCommand_with_cpu_starts_the_sampler_using_local_config_interval_by_default()
    {
        ArrangeEnabled(10000);

        var result = _service.StartFromCommand(new[] { "cpu" }, sampleIntervalMs: null, cpuReportIntervalMs: null);

        Mock.Assert(() => _native.Start(10000), Occurs.Once());
        Assert.Multiple(() =>
        {
            Assert.That(_service.IsActive, Is.True);
            Assert.That(result.ActiveTypes, Is.EqualTo(new[] { "cpu" }));
            Assert.That(result.Exceptions, Is.Empty);
        });
    }

    [Test]
    public void StartFromCommand_with_cpu_report_interval_overrides_local_config_and_clamps_to_bounds()
    {
        ArrangeEnabled(10000);

        var result = _service.StartFromCommand(new[] { "cpu" }, sampleIntervalMs: null, cpuReportIntervalMs: 500);

        // 500ms is below the 1000ms floor -- clamped up, matching DefaultConfiguration's own clamp.
        Mock.Assert(() => _native.Start(1000), Occurs.Once());
        Assert.That(result.SampleIntervalMs, Is.EqualTo(1000));
        Assert.That(result.CpuReportIntervalMs, Is.EqualTo(1000));
    }

    [Test]
    public void StartFromCommand_while_already_active_is_an_idempotent_noop()
    {
        ArrangeEnabled(10000);
        _service.StartFromCommand(new[] { "cpu" }, null, null);

        var result = _service.StartFromCommand(new[] { "cpu" }, sampleIntervalMs: null, cpuReportIntervalMs: 30000);

        // Already running -- a repeat start does not retune, per the spec's idempotent-no-op requirement.
        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Once());
        Assert.That(result.Exceptions, Is.Empty);
    }

    [Test]
    public void StartFromCommand_with_cpu_reports_the_native_start_failure_instead_of_swallowing_it()
    {
        // H5: a genuine runtime start failure must reach the command result's Exceptions, not just the
        // log -- StartFromCommand's caller (StartContinuousProfilerCommand) has no other way to learn
        // the command actually failed.
        Mock.Arrange(() => _native.Start(Arg.AnyInt)).Throws(new InvalidOperationException("boom"));
        ArrangeEnabled(10000);

        var result = _service.StartFromCommand(new[] { "cpu" }, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(_service.IsActive, Is.False);
            Assert.That(result.Exceptions["cpu"], Is.EqualTo("boom"));
        });
    }

    [Test]
    public void StartFromCommand_with_heap_reports_not_supported_and_does_not_start_anything()
    {
        ArrangeEnabled(10000);

        var result = _service.StartFromCommand(new[] { "heap" }, null, null);

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Never());
        Assert.Multiple(() =>
        {
            Assert.That(_service.IsActive, Is.False);
            Assert.That(result.Exceptions["heap"], Is.EqualTo("not supported"));
        });
    }

    [Test]
    public void StartFromCommand_with_all_starts_cpu_and_reports_heap_as_not_supported()
    {
        ArrangeEnabled(10000);

        var result = _service.StartFromCommand(new[] { "all" }, null, null);

        Mock.Assert(() => _native.Start(10000), Occurs.Once());
        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveTypes, Is.EqualTo(new[] { "cpu" }));
            Assert.That(result.Exceptions["heap"], Is.EqualTo("not supported"));
        });
    }

    [Test]
    public void StartFromCommand_with_unknown_token_reports_it_as_not_supported()
    {
        ArrangeEnabled(10000);

        var result = _service.StartFromCommand(new[] { "bogus" }, null, null);

        Assert.That(result.Exceptions["bogus"], Is.EqualTo("not supported"));
    }

    [Test]
    public void StartFromCommand_with_empty_include_is_a_query_that_changes_nothing()
    {
        ArrangeEnabled(10000);

        var result = _service.StartFromCommand(Array.Empty<string>(), null, null);

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Never());
        Assert.That(_service.IsActive, Is.False);
        Assert.That(result.ActiveTypes, Is.Empty);
    }

    [Test]
    public void StartFromCommand_with_cpu_under_high_security_mode_reports_not_supported_and_does_not_start_anything()
    {
        ArrangeEnabled(10000, highSecurityModeEnabled: true);

        var result = _service.StartFromCommand(new[] { "cpu" }, null, null);

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Never());
        Assert.Multiple(() =>
        {
            Assert.That(_service.IsActive, Is.False);
            Assert.That(result.Exceptions["cpu"], Is.EqualTo("not supported: high security mode enabled"));
        });
    }

    [Test]
    public void StartFromCommand_with_all_under_high_security_mode_reports_cpu_and_heap_as_not_supported()
    {
        ArrangeEnabled(10000, highSecurityModeEnabled: true);

        var result = _service.StartFromCommand(new[] { "all" }, null, null);

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Never());
        Assert.Multiple(() =>
        {
            Assert.That(_service.IsActive, Is.False);
            Assert.That(result.Exceptions["cpu"], Is.EqualTo("not supported: high security mode enabled"));
            Assert.That(result.Exceptions["heap"], Is.EqualTo("not supported"));
        });
    }

    [Test]
    public void StartFromCommand_under_high_security_mode_does_not_mark_cpu_command_controlled()
    {
        ArrangeEnabled(10000, highSecurityModeEnabled: true);
        _service.StartFromCommand(new[] { "cpu" }, null, null);

        // HSM lifted: a subsequent config-driven ApplyConfigChange must still be able to start -- the
        // rejected command start must not have claimed command ownership of cpu with nothing there to
        // release it.
        var reconfigured = Mock.Create<IConfiguration>();
        Mock.Arrange(() => reconfigured.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => reconfigured.ContinuousProfilingSamplingIntervalMs).Returns(10000);
        Mock.Arrange(() => reconfigured.ApplicationNames).Returns(new[] { "MyApp" });
        Mock.Arrange(() => reconfigured.HighSecurityModeEnabled).Returns(false);
        _service.OverrideConfigForTesting(reconfigured);
        _service.ApplyConfigChange();

        Mock.Assert(() => _native.Start(10000), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void StartFromCommand_deferred_behind_thread_profiling_actually_starts_once_it_finishes()
    {
        ArrangeEnabled(10000);

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(true);
        _service.ThreadProfilingStatus = tpStatus;

        Action retry = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => retry = action);

        var result = _service.StartFromCommand(new[] { "cpu" }, sampleIntervalMs: null, cpuReportIntervalMs: 5000);

        // Deferred: no native start yet, but a retry was scheduled and command ownership was still claimed.
        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Never());
        Assert.That(_service.IsActive, Is.False);
        Assert.That(result.Exceptions, Is.Empty);
        Assert.That(retry, Is.Not.Null);

        // Thread profiling finishes; firing the ACTUAL scheduled retry (not re-issuing the command) must
        // start CP -- going through ApplyConfigChange instead would no-op here because
        // _commandControlledTypes still holds "cpu" from the StartFromCommand call above (see H2).
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(false);
        retry.Invoke();

        Mock.Assert(() => _native.Start(5000), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void StartFromCommand_deferred_retry_is_a_noop_if_stop_command_released_ownership_first()
    {
        ArrangeEnabled(10000);

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(true);
        _service.ThreadProfilingStatus = tpStatus;

        Action retry = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => retry = action);

        _service.StartFromCommand(new[] { "cpu" }, null, null);

        // Operator changes their mind and stops before the deferred retry ever fires.
        _service.StopFromCommand(new[] { "cpu" });

        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(false);
        retry.Invoke();

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Never());
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void StopFromCommand_with_cpu_stops_an_active_session()
    {
        ArrangeEnabled(10000);
        _service.StartFromCommand(new[] { "cpu" }, null, null);

        var result = _service.StopFromCommand(new[] { "cpu" });

        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Assert.Multiple(() =>
        {
            Assert.That(_service.IsActive, Is.False);
            Assert.That(result.ActiveTypes, Is.Empty);
        });
    }

    [Test]
    public void StopFromCommand_while_not_active_is_an_idempotent_noop()
    {
        ArrangeEnabled(10000);

        var result = _service.StopFromCommand(new[] { "cpu" });

        Mock.Assert(() => _native.Stop(), Occurs.Never());
        Assert.That(result.Exceptions, Is.Empty);
    }

    [Test]
    public void StopFromCommand_with_empty_include_stops_the_active_session()
    {
        // M11: an empty/absent "include" on a stop command means "stop everything currently active or
        // command-controlled", not "nothing to stop" -- asymmetric with start, where an empty include is
        // a no-op query (see StartFromCommand_with_empty_include_is_a_query_that_changes_nothing).
        ArrangeEnabled(10000);
        _service.StartFromCommand(new[] { "cpu" }, null, null);

        var result = _service.StopFromCommand(Array.Empty<string>());

        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Assert.Multiple(() =>
        {
            Assert.That(_service.IsActive, Is.False);
            Assert.That(result.ActiveTypes, Is.Empty);
            Assert.That(result.Exceptions, Is.Empty);
        });
    }

    [Test]
    public void StopFromCommand_with_empty_include_while_not_active_is_still_a_success_noop()
    {
        ArrangeEnabled(10000);

        var result = _service.StopFromCommand(Array.Empty<string>());

        Mock.Assert(() => _native.Stop(), Occurs.Never());
        Assert.That(result.Exceptions, Is.Empty);
    }

    [Test]
    public void A_stop_command_is_not_undone_by_a_subsequent_config_update_reporting_enabled()
    {
        // M10: an operator's stop_continuous_profiler command must survive the next
        // ConfigurationUpdatedEvent (e.g. a reconnect) as long as server-side config still says
        // enabled=true -- otherwise the very next reconciliation resurrects the session the operator
        // just explicitly stopped.
        ArrangeEnabled(10000);
        _service.StartFromCommand(new[] { "cpu" }, null, null);

        _service.StopFromCommand(new[] { "cpu" });
        Mock.Assert(() => _native.Stop(), Occurs.Once());

        // Config never changed -- ContinuousProfilingEnabled is still true -- yet the reconciliation
        // that a reconnect's ConfigurationUpdatedEvent triggers must not restart the stopped session.
        _service.ApplyConfigChange();

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void An_explicit_start_command_lifts_a_prior_stop_commands_suppression()
    {
        ArrangeEnabled(10000);
        _service.StartFromCommand(new[] { "cpu" }, null, null);
        _service.StopFromCommand(new[] { "cpu" });

        // The operator changes their mind again and explicitly restarts -- this must succeed and must
        // also clear the suppression a plain config update alone could not lift.
        _service.StartFromCommand(new[] { "cpu" }, null, null);

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Exactly(2));
        Assert.That(_service.IsActive, Is.True);

        _service.StopFromCommand(new[] { "cpu" });
        _service.ApplyConfigChange();

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Exactly(2));
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void A_command_started_session_is_immune_to_an_unrelated_config_update_until_explicitly_stopped()
    {
        ArrangeEnabled(10000);
        _service.StartFromCommand(new[] { "cpu" }, null, null);

        // Simulate an unrelated config-update event (e.g. a reconnect, or an SSC push for something else)
        // that would otherwise stop CP because local config still reports it disabled.
        var disabledConfig = Mock.Create<IConfiguration>();
        Mock.Arrange(() => disabledConfig.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(disabledConfig);
        _service.ApplyConfigChange();

        Mock.Assert(() => _native.Stop(), Occurs.Never());
        Assert.That(_service.IsActive, Is.True);

        // An explicit stop command releases ownership; a subsequent config update can act again.
        _service.StopFromCommand(new[] { "cpu" });
        _service.ApplyConfigChange(); // config still says disabled -- this is now a legitimate no-op stop path, already stopped

        Mock.Assert(() => _native.Stop(), Occurs.Once());
    }

    [Test]
    public void StartLocked_starts_the_native_profiler_at_the_configured_interval()
    {
        ArrangeEnabled(10000);

        _service.StartIfEnabled();

        Mock.Assert(() => _native.Start(10000), Occurs.Once());
    }

    [Test]
    public void StartLocked_unwinds_to_inactive_when_the_native_start_throws()
    {
        // IsActive is armed before the native start so the thread-profiling guard sees it as early as
        // possible; a failed start must therefore unwind it, or nothing would ever start again.
        Mock.Arrange(() => _native.Start(Arg.AnyInt)).Throws(new InvalidOperationException("boom"));
        ArrangeEnabled(10000);

        _service.StartIfEnabled();

        Assert.That(_service.IsActive, Is.False);
        Mock.Assert(() => _native.Stop(), Occurs.Once());
    }

    [Test]
    public void StartLocked_failure_unwind_runs_outside_the_mutual_exclusion_gate()
    {
        // M4 (2026-09 review): the native-start failure path must call StopLocked OUTSIDE
        // ProfilingMutualExclusionGate.Acquire(). StopLocked can block for up to _drainShutdownWaitTimeout
        // (60s default) on an in-flight drain or a concurrent stop; holding the process-wide Gate across
        // that unwind would stall ThreadProfilingService.StartThreadProfilingSession -- which takes the
        // same Gate synchronously on the agent-command thread -- and the whole command batch behind it.
        // Prove the Gate is free while the unwind's _native.Stop() runs (against the old code, where the
        // catch called StopLocked while still holding the Gate, the probe below would time out).
        ArrangeEnabled(10000);
        Mock.Arrange(() => _native.Start(Arg.AnyInt)).Throws(new InvalidOperationException("boom"));

        var gateFreeDuringUnwind = false;
        Mock.Arrange(() => _native.Stop()).DoInstead(() =>
        {
            // StopLocked is executing now (it calls _native.Stop()). Contend for the Gate from another
            // thread: if StartLocked still held it across the unwind, this acquire would block until the
            // unwind returned rather than succeeding immediately.
            var acquired = false;
            var probe = new Thread(() =>
            {
                using (ProfilingMutualExclusionGate.Acquire())
                {
                    acquired = true;
                }
            }) { IsBackground = true };
            probe.Start();
            probe.Join(TimeSpan.FromSeconds(5));
            gateFreeDuringUnwind = acquired;
        });

        _service.StartIfEnabled();

        Assert.That(gateFreeDuringUnwind, Is.True, "StopLocked's failure unwind must not hold ProfilingMutualExclusionGate.Lock");
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void StopLocked_stops_the_native_profiler()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var disabled = Mock.Create<IConfiguration>();
        Mock.Arrange(() => disabled.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(disabled);
        _service.ApplyConfigChange();

        Mock.Assert(() => _native.Stop(), Occurs.Once());
    }

    [Test]
    public void Retune_stops_then_restarts_the_native_profiler_at_the_new_interval()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var retuned = Mock.Create<IConfiguration>();
        Mock.Arrange(() => retuned.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => retuned.ContinuousProfilingSamplingIntervalMs).Returns(20000);
        Mock.Arrange(() => retuned.ApplicationNames).Returns(new[] { "MyApp" });
        _service.OverrideConfigForTesting(retuned);
        _service.ApplyConfigChange();

        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Mock.Assert(() => _native.Start(20000), Occurs.Once());
    }

    [Test]
    public void A_service_constructed_while_disabled_can_still_be_live_enabled_later()
    {
        // Regression test: the factory now always constructs the service, even when continuous profiling
        // is disabled at startup. This mirrors that -- construction (via SetUp) plus a disabled config --
        // and proves the object still reacts to a later live config change, exactly as ApplyConfigChange
        // is invoked from OnConfigurationUpdated for every other config-reactive service.
        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(_config);

        _service.StartIfEnabled();

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Never());
        Assert.That(_service.IsActive, Is.False);

        ArrangeEnabled(10000);
        _service.ApplyConfigChange();

        Mock.Assert(() => _native.Start(10000), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void ApplyConfigChange_enabling_from_disabled_starts()
    {
        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(_config);
        _service.StartIfEnabled();
        Assert.That(_service.IsActive, Is.False);

        ArrangeEnabled(10000);
        _service.ApplyConfigChange();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(10000), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void Drain_tick_with_no_data_does_not_send()
    {
        // Must start first: the drain buffer is now allocated lazily on session start, so an unstarted
        // service's DrainOnce short-circuits before ReadBatch (that gate is covered by its own test below).
        ArrangeEnabled(10000);
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns(0);

        _service.DrainOnce();

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
    }

    [Test]
    public void Drain_tick_with_data_parses_builds_and_sends()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var batch = OneSampleBatch("worker-1", 4242, 0x11, 0x22, 0x33, new[] { "A.B.Leaf()", "A.B.Root()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });

        _service.DrainOnce();

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Once());
    }

    [Test]
    public void Drain_tick_with_data_reports_supportability_metric()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var batch = OneSampleBatch("worker-1", 1, 0, 0, 0, new[] { "F()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });

        _service.DrainOnce();

        // Pin the exact counts: the batch carries one sample, so Samples must report 1 (not "any long"),
        // and Drain reports its default count of 1. Occurs.Once() also catches a duplicate report.
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Drain", 1), Occurs.Once());
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Samples", 1), Occurs.Once());
    }

    [Test]
    public void Drain_tick_with_a_failed_send_reports_the_error_metric_not_drain_or_samples()
    {
        // A dropped profile isn't a healthy drain -- Drain/Samples must not fire on a failed send, and the
        // failure should route to the same error metric the other defensive branches use.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);
        ArrangeReadableBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();

        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Error"), Occurs.Once());
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Drain"), Occurs.Never());
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Samples", Arg.IsAny<long>()), Occurs.Never());
    }

    [Test]
    public void Drain_tick_with_bytesRead_exceeding_buffer_length_is_discarded()
    {
        // Start first so the drain buffer is allocated (lazy since H2); otherwise DrainOnce short-circuits
        // before ReadBatch and the discard branch is never reached.
        ArrangeEnabled(10000);
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) => dest.Length + 1);

        Assert.DoesNotThrow(() => _service.DrainOnce());
        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
    }

    [Test]
    public void Drain_tick_with_an_unknown_batch_version_reports_the_error_metric_and_sends_nothing()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        using var s = new MemoryStream();
        s.WriteByte(StartBatch); s.WriteByte(99); WriteLong(s, 123456789L); // unknown/future version
        s.WriteByte(EndBatch);
        var batch = s.ToArray();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });

        _service.DrainOnce();

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Error"), Occurs.Once());
    }

    [Test]
    public void Drain_tick_that_fills_the_entire_drain_buffer_reports_the_boundary_metric()
    {
        // Simulates native having filled (or exceeded, then been clamped by ReadBatch itself) the whole
        // managed buffer -- the tripwire for the two buffer-size constants drifting apart again.
        // Start first so the drain buffer is allocated (lazy since H2).
        ArrangeEnabled(10000);
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) => dest.Length);

        _service.DrainOnce();

        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/DrainBufferBoundary"), Occurs.Once());
    }

    [Test]
    public void Drain_tick_with_a_small_batch_does_not_report_the_boundary_metric()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();
        ArrangeReadableBatch();

        _service.DrainOnce();

        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/DrainBufferBoundary"), Occurs.Never());
    }

    [Test]
    public void Drain_tick_never_throws_when_source_throws()
    {
        // Start first so the drain buffer is allocated (lazy since H2) and ReadBatch is actually reached.
        ArrangeEnabled(10000);
        _service.StartIfEnabled();
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _service.DrainOnce());
        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
    }

    [Test]
    public void Drain_tick_never_throws_when_transport_throws()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var batch = OneSampleBatch("worker-1", 1, 0, 0, 0, new[] { "F()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });
        Mock.Arrange(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()))
            .Throws(new InvalidOperationException("send failed"));

        Assert.DoesNotThrow(() => _service.DrainOnce());
    }

    [Test]
    public void A_second_concurrent_DrainOnce_is_a_no_op_while_one_is_already_in_flight()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);

        var readCount = 0;
        var innerDrainRan = false;
        var batch = OneSampleBatch("worker-1", 1, 0, 0, 0, new[] { "F()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            readCount++;
            // Simulate a second timer tick landing while this drain is still "in flight" -- the read
            // itself is the earliest point at which a real concurrent drain would already be racing
            // this one over _drainBuffer, so re-entering DrainOnce from here is the tightest simulation
            // available without a real second thread.
            if (readCount == 1)
            {
                service.DrainOnce();
                innerDrainRan = true;
            }
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });

        service.DrainOnce();

        Assert.That(innerDrainRan, Is.True, "the test setup itself should have triggered the nested call");
        Assert.That(readCount, Is.EqualTo(1), "the nested/concurrent DrainOnce must not have read the buffer a second time");
    }

    [Test]
    public void A_drain_whose_body_throws_still_releases_the_in_flight_guard_for_the_next_tick()
    {
        // The guard is released in a finally, and the CompareExchange that takes it is immediately
        // followed by that try -- so no throw anywhere in a drain can leak it. A leak would be permanent:
        // every later tick would lose the guard and no-op for the rest of the process's life.
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var readCount = 0;
        var batch = OneSampleBatch("worker-1", 1, 0, 0, 0, new[] { "F()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            readCount++;
            if (readCount == 1)
                throw new InvalidOperationException("boom");

            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });

        _service.DrainOnce(); // throws inside the drain body; swallowed by DrainOnce's own catch
        _service.DrainOnce();

        Assert.That(readCount, Is.EqualTo(2), "the second tick must not have been skipped as 'a drain is already in flight'");
        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Once());
    }

    [Test]
    public void DrainOnce_releases_the_in_flight_guard_so_the_next_tick_is_not_skipped()
    {
        // The release (now Interlocked.Exchange, matching the acquire side's CompareExchange) must still
        // clear the guard back to "idle" so a normal, non-overlapping next tick is never mistaken for a
        // still-in-flight drain and skipped.
        ArrangeEnabled(10000);
        _service.StartIfEnabled();
        ArrangeReadableBatch();

        _service.DrainOnce();
        _service.DrainOnce();

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(2));
    }

    #region H2 -- lazy drain-buffer allocation

    [Test]
    public void Drain_buffer_is_not_allocated_when_constructed_and_never_started()
    {
        // The multi-MB LOH drain buffer must not be allocated in the constructor: a process that never
        // enables continuous profiling (the default) should pay zero LOH for it.
        using var service = new ContinuousProfilingService(_source, _native, _transport, _scheduler, _health);

        Assert.That(service.IsDrainBufferAllocatedForTesting, Is.False);
    }

    [Test]
    public void Starting_a_session_allocates_a_drain_buffer_of_exactly_four_megabytes()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        Assert.That(_service.IsDrainBufferAllocatedForTesting, Is.True);

        // The array actually handed to the sample source must be the full 4 MB buffer, not a smaller one.
        byte[] captured = null;
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) => { captured = dest; return 0; });

        _service.DrainOnce();

        Assert.That(captured, Is.Not.Null, "the drain must have handed a buffer to ReadBatch");
        Assert.That(captured.Length, Is.EqualTo(4 * 1024 * 1024));
    }

    [Test]
    public void Stopping_a_session_releases_the_drain_buffer()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();
        Assert.That(_service.IsDrainBufferAllocatedForTesting, Is.True);

        var disabled = Mock.Create<IConfiguration>();
        Mock.Arrange(() => disabled.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(disabled);
        _service.ApplyConfigChange();

        Assert.That(_service.IsDrainBufferAllocatedForTesting, Is.False, "the LOH buffer must be released on stop");
    }

    [Test]
    public void Stopping_then_starting_again_reallocates_the_buffer_and_a_drain_still_works()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var disabled = Mock.Create<IConfiguration>();
        Mock.Arrange(() => disabled.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(disabled);
        _service.ApplyConfigChange();
        Assert.That(_service.IsDrainBufferAllocatedForTesting, Is.False);

        // Re-enable: the buffer is reallocated and the drain path is live again end to end.
        ArrangeEnabled(10000);
        _service.ApplyConfigChange();
        Assert.That(_service.IsDrainBufferAllocatedForTesting, Is.True);

        ArrangeReadableBatch();
        _service.DrainOnce();

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Once());
    }

    [Test]
    public void DrainOnce_with_no_buffer_allocated_returns_without_reading_and_without_throwing()
    {
        // _service is connected (SetUp) but never started, so the lazy buffer is null. DrainOnce must
        // short-circuit before ReadBatch rather than NullReferenceException on the buffer.
        Assert.That(_service.IsDrainBufferAllocatedForTesting, Is.False);

        Assert.DoesNotThrow(() => _service.DrainOnce());

        Mock.Assert(() => _source.ReadBatch(Arg.IsAny<byte[]>()), Occurs.Never(), "a drain with no buffer allocated must not read the native sample buffer");
    }

    [Test]
    public void A_retune_reallocates_the_buffer_and_the_drain_still_works_afterward()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        // A config-driven interval change stops and restarts native sampling (a retune), which frees and
        // reallocates the buffer.
        var retuned = Mock.Create<IConfiguration>();
        Mock.Arrange(() => retuned.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => retuned.ContinuousProfilingSamplingIntervalMs).Returns(20000);
        Mock.Arrange(() => retuned.ApplicationNames).Returns(new[] { "MyApp" });
        _service.OverrideConfigForTesting(retuned);
        _service.ApplyConfigChange();

        Assert.That(_service.IsDrainBufferAllocatedForTesting, Is.True);

        ArrangeReadableBatch();
        _service.DrainOnce();

        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Once());
    }

    #endregion

    [Test]
    public void StartIfEnabled_serializes_on_ProfilingMutualExclusionGate()
    {
        // Proves StartLocked's guard-check-and-arm sequence actually takes
        // ProfilingMutualExclusionGate.Acquire() -- the same lock ThreadProfilingService.
        // StartThreadProfilingSession takes -- rather than merely documenting the intent in a comment.
        ArrangeEnabled(10000);

        Task startTask;

        // Signaled by the background task the instant before it calls StartIfEnabled, so the assertion
        // below runs only after the worker is provably executing -- not while it may still be sitting
        // unscheduled in the ThreadPool queue (which would let the "did not complete" check pass without
        // the gate ever being contended).
        using var workerReachedStart = new ManualResetEventSlim(false);

        using (ProfilingMutualExclusionGate.Acquire())
        {
            startTask = Task.Run(() =>
            {
                workerReachedStart.Set();
                _service.StartIfEnabled();
            });

            Assert.That(workerReachedStart.Wait(5000), Is.True, "Background worker never started.");

            var completedWhileHeld = Task.WaitAny(new Task[] { startTask }, 200) == 0;
            Assert.That(completedWhileHeld, Is.False, "StartIfEnabled must block while the gate is held elsewhere.");
        }

        Assert.That(startTask.Wait(5000), Is.True, "StartIfEnabled must complete once the gate is released.");
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void StartIfEnabled_defers_when_thread_profiling_active()
    {
        ArrangeEnabled(10000);

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(true);
        _service.ThreadProfilingStatus = tpStatus;

        _service.StartIfEnabled();

        // Deferred: no recurring drain scheduled, not active, but a retry was scheduled.
        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Never());
        Mock.Assert(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()), Occurs.AtLeast(1));
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void StartIfEnabled_starts_when_thread_profiling_inactive()
    {
        ArrangeEnabled(10000);

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(false);
        _service.ThreadProfilingStatus = tpStatus;

        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(10000), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void ApplyConfigChange_defers_start_when_thread_profiling_active()
    {
        ArrangeEnabled(10000);

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(true);
        _service.ThreadProfilingStatus = tpStatus;

        _service.ApplyConfigChange();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Never());
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void Deferred_start_activates_once_thread_profiling_finishes()
    {
        ArrangeEnabled(10000);

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(true);
        _service.ThreadProfilingStatus = tpStatus;

        // First attempt defers (TP active) and schedules a retry.
        _service.StartIfEnabled();
        Assert.That(_service.IsActive, Is.False);

        // TP finishes; a retry now succeeds.
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(false);
        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(10000), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void Dispose_stops_scheduled_drain_when_active()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        _service.Dispose();

        Mock.Assert(() => _scheduler.StopExecuting(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan?>()), Occurs.Once());
    }

    [Test]
    public void Dispose_shuts_down_the_native_profiler()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        _service.Dispose();

        Mock.Assert(() => _native.Shutdown(), Occurs.Once());
    }

    [Test]
    public void Dispose_shuts_down_the_native_profiler_even_when_never_started()
    {
        _service.Dispose();

        Mock.Assert(() => _native.Shutdown(), Occurs.Once());
    }

    [Test]
    public void Dispose_does_not_throw_when_native_shutdown_throws()
    {
        Mock.Arrange(() => _native.Shutdown()).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _service.Dispose());
    }

    [Test]
    public void Starting_enables_the_process_wide_trace_context()
    {
        ArrangeEnabled(10000);

        _service.StartIfEnabled();

        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.True);
    }

    [Test]
    public void Stopping_disables_the_process_wide_trace_context()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        var disabled = Mock.Create<IConfiguration>();
        Mock.Arrange(() => disabled.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(disabled);
        _service.ApplyConfigChange();

        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.False);
    }

    [Test]
    public void Dispose_disables_the_process_wide_trace_context()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        _service.Dispose();

        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.False);
    }

    [Test]
    public void CountOnCpu_countsOnlyOnCpuSamples()
    {
        var samples = new List<ManagedThreadSample>
        {
            new ManagedThreadSample("a", 1, 0, 0, 0, new[] { "F" }, onCpu: true),
            new ManagedThreadSample("b", 2, 0, 0, 0, new[] { "F" }, onCpu: false),
            new ManagedThreadSample("c", 3, 0, 0, 0, new[] { "F" }, onCpu: true),
        };
        Assert.That(ContinuousProfilingService.CountOnCpu(samples), Is.EqualTo(2));
    }

    [Test]
    public void CountOnCpu_emptyList_isZero()
    {
        Assert.That(ContinuousProfilingService.CountOnCpu(new List<ManagedThreadSample>()), Is.EqualTo(0));
    }

    [Test]
    public void CountOnCpu_allOff_isZero()
    {
        var samples = new List<ManagedThreadSample> { new ManagedThreadSample("a", 1, 0, 0, 0, new[] { "F" }, onCpu: false) };
        Assert.That(ContinuousProfilingService.CountOnCpu(samples), Is.EqualTo(0));
    }

    #region AgentConnectedEvent -- collector-endpoint resolution

    [Test]
    public void AgentConnected_updates_the_transport_to_the_collector_endpoint()
    {
        // A dedicated transport mock: _transport is shared with _service (already connected in SetUp),
        // whose own handler would also fire on this test's Publish call and double-count the assertion.
        var transport = Mock.Create<IProfilesTransport>();
        using var service = new ContinuousProfilingService(_source, _native, transport, _scheduler, _health);

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.eu01.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);

        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        Mock.Assert(() => transport.UpdateEndpoint("https://collector.eu01.nr-data.net/v1/profiles"), Occurs.Once());
    }

    [Test]
    public void AgentConnected_does_not_update_the_transport_when_connect_info_has_no_host()
    {
        // Dedicated transport mock -- see note above.
        var transport = Mock.Create<IProfilesTransport>();
        using var service = new ContinuousProfilingService(_source, _native, transport, _scheduler, _health);

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.Host).Returns(string.Empty);

        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        Mock.Assert(() => transport.UpdateEndpoint(Arg.IsAny<string>()), Occurs.Never());
    }

    [Test]
    public void DrainOnce_before_the_agent_has_connected_drops_without_reading_the_native_buffer()
    {
        // A fresh, never-connected instance -- distinct from _service (SetUp already connects it) --
        // so this proves the pre-connect gate, not just "nothing to drain."
        using var service = new ContinuousProfilingService(_source, _native, _transport, _scheduler, _health);

        service.DrainOnce();

        Mock.Assert(() => _source.ReadBatch(Arg.IsAny<byte[]>()), Occurs.Never());
        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
    }

    [Test]
    public void DrainOnce_after_Dispose_reads_nothing_and_ships_nothing()
    {
        // Regression test for the missing _disposed gate: AgentManager disposes CP before the container-
        // owned Scheduler, so a queued drain tick can fire after Dispose has already joined the native
        // worker thread. Without the gate it P/Invokes into a dead sampler and ships one last profile.
        var (service, transport) = NewConnectedService();
        EnableAndStart(service);
        ArrangeReadableBatch();

        service.Dispose();
        service.DrainOnce();

        Mock.Assert(() => _source.ReadBatch(Arg.IsAny<byte[]>()), Occurs.Never(), "a drain after Dispose must not touch the native sample buffer");
        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never(), "a drain after Dispose must not ship a profile");
    }

    [Test]
    public void AgentConnected_landing_after_Dispose_does_not_set_the_endpoint()
    {
        // Regression test for the missing _disposed gate in OnAgentConnected. Reproduces the exact race
        // window -- _disposed already set, but base.Dispose() hasn't yet removed the subscription -- by
        // publishing the connect from inside the mocked _native.Shutdown(), which Dispose calls after
        // setting _disposed and before unsubscribing. Without the gate this would set _isConnected and the
        // profiles endpoint on an already-disposed service, letting a queued drain ship a profile.
        var transport = Mock.Create<IProfilesTransport>();
        var service = new ContinuousProfilingService(_source, _native, transport, _scheduler, _health);

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        Mock.Arrange(() => _native.Shutdown()).DoInstead(() =>
            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo }));

        service.Dispose();

        Mock.Assert(() => transport.UpdateEndpoint(Arg.IsAny<string>()), Occurs.Never(), "a connect landing after _disposed is set must not resolve/set the profiles endpoint");
    }

    #endregion

    #region Send-failure backoff

    // A dedicated (service, transport) pair per test, already connected: SetUp's blanket
    // `_transport.Send(...) => true` arrangement is a fixture-wide default these tests need to override
    // per call, and re-arranging the SAME shared mock in a test body does not take precedence over the
    // arrangement already registered in SetUp (empirically -- the earlier-registered match wins). A fresh
    // transport mock has no prior arrangement to compete with.
    private (ContinuousProfilingService Service, IProfilesTransport Transport) NewConnectedService()
    {
        var transport = Mock.Create<IProfilesTransport>();
        var service = new ContinuousProfilingService(_source, _native, transport, _scheduler, _health);

        // DrainOnce reads _configuration (ApplicationNames, ContinuousProfilingIncludeAgentCode) on its
        // way to Send. Without this, the service falls back to the real DefaultConfiguration.Instance
        // (unsafe outside the full agent harness) and DrainOnce's outer catch silently swallows the
        // resulting exception -- Send is never reached, and every test here would look like it passed
        // for the wrong reason (no failure ever recorded because no send ever happened).
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        Mock.Arrange(() => _config.ContinuousProfilingIncludeAgentCode).Returns(false);
        service.OverrideConfigForTesting(_config);

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        return (service, transport);
    }

    private void ArrangeReadableBatch()
    {
        var batch = OneSampleBatch("worker-1", 1, 0, 0, 0, new[] { "F()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });
    }

    private void EnableAndStart(ContinuousProfilingService service, int intervalMs = 10000)
    {
        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _config.ContinuousProfilingSamplingIntervalMs).Returns(intervalMs);
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();
    }

    [Test]
    public void One_send_failure_alone_does_not_trip_backoff()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service); // allocate the lazy drain buffer (H2) so the drain actually reaches Send
        ArrangeReadableBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();

        Mock.Assert(() => _native.Stop(), Occurs.Never());
        Mock.Assert(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()), Occurs.Never());
    }

    [Test]
    public void Two_consecutive_send_failures_trip_backoff_at_the_first_step()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service); // allocate the lazy drain buffer (H2) so the drain actually reaches Send
        ArrangeReadableBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce();

        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(2));
        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Mock.Assert(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), TimeSpan.FromSeconds(15)), Occurs.Once());
    }

    [Test]
    public void A_thrown_send_exception_counts_as_a_failure()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service); // allocate the lazy drain buffer (H2) so the drain actually reaches Send
        ArrangeReadableBatch();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()))
            .Throws(new InvalidOperationException("send failed"));

        service.DrainOnce();
        service.DrainOnce();

        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Mock.Assert(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), TimeSpan.FromSeconds(15)), Occurs.Once());
    }

    [Test]
    public void A_success_after_tripping_fully_resets_the_backoff_index()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;

        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _config.ContinuousProfilingSamplingIntervalMs).Returns(10000);
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();

        ArrangeReadableBatch();

        Action probe = null;
        var delays = new List<TimeSpan>();
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => { probe = action; delays.Add(delay); });

        // First trip: two failures -> backs off at the first step (15s).
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);
        service.DrainOnce();
        service.DrainOnce();
        Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(15) }));

        // The probe fires: resumes sampling, clears the gate.
        probe.Invoke();

        // A single successful drain resets the failure/backoff state.
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();

        // Two more failures should back off at the FIRST step again (15s), not an advanced one.
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);
        service.DrainOnce();
        service.DrainOnce();

        Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15) }));
    }

    [Test]
    public void DrainOnce_while_backoff_is_active_drops_without_reading_the_native_buffer()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service); // allocate the lazy drain buffer (H2) so the first two drains actually read

        var readCount = 0;
        var batch = OneSampleBatch("worker-1", 1, 0, 0, 0, new[] { "F()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            readCount++;
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce(); // failure 1
        service.DrainOnce(); // failure 2 -> trips backoff
        Assert.That(readCount, Is.EqualTo(2));

        service.DrainOnce(); // gated -- must not touch the native buffer

        Assert.That(readCount, Is.EqualTo(2));
    }

    [Test]
    public void DrainOnce_skips_Send_when_every_sample_is_filtered_as_agent_code()
    {
        // ContinuousProfilingIncludeAgentCode:false (NewConnectedService's default) filters every sample
        // whose leaf frame is agent-owned. When that empties the whole batch, OtlpProfileBuilder still
        // returns a request with zero Profiles under its ScopeProfiles -- DrainOnce must not POST that (P8).
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);

        var batch = OneSampleBatch("worker-1", 1, 0, 0, 0, new[] { "NewRelic.Agent.Core.SomeAgentMethod()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });

        service.DrainOnce();

        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never(), "an all-filtered batch must not be sent");
    }

    [Test]
    public void A_drain_that_outruns_a_stops_bounded_wait_does_not_send_an_empty_profile()
    {
        // StopLocked's finally zeroes _activeIntervalMs, so a drain that outran that stop's bounded wait
        // reaches the build step with no period. OtlpProfileBuilder emits no profiles at all without one,
        // so continuing would POST a payload carrying nothing -- and once a restart has cleared
        // _stopSignaled, OnSendResult scores that empty POST's HTTP outcome as a real send success or
        // failure in the backoff state machine.
        //
        // The stop is triggered from inside ReadBatch: that is exactly where a real drain sits while the
        // stopper's bounded wait for it expires, and re-entering on this one thread makes the interleaving
        // deterministic. A 50ms wait bound keeps the timeout it must hit cheap.
        var transport = Mock.Create<IProfilesTransport>();
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        using var service = new ContinuousProfilingService(_source, _native, transport, _scheduler, _health, TimeSpan.FromMilliseconds(50));

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _config.ContinuousProfilingSamplingIntervalMs).Returns(10000);
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        Mock.Arrange(() => _config.ContinuousProfilingIncludeAgentCode).Returns(false);
        service.OverrideConfigForTesting(_config);
        service.StartIfEnabled();

        var disabled = Mock.Create<IConfiguration>();
        Mock.Arrange(() => disabled.ContinuousProfilingEnabled).Returns(false);

        var readCount = 0;
        var batch = OneSampleBatch("worker-1", 1, 0, 0, 0, new[] { "F()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            readCount++;
            // A config-disable stop lands while this drain is in flight. Its bounded wait is waiting on
            // this very drain, so it times out and tears the session down (interval zeroed, buffer
            // released) before ReadBatch below returns a real batch to the still-running drain.
            service.OverrideConfigForTesting(disabled);
            service.ApplyConfigChange();

            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });

        service.DrainOnce();

        Assert.That(readCount, Is.EqualTo(1), "precondition: the drain read a real batch");
        Assert.That(service.IsActive, Is.False, "precondition: the racing stop tore the session down while this drain was in flight");
        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never(),
            "a drain left with no sampling interval must be dropped, not POSTed as an empty profile whose outcome feeds the backoff state machine");
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Error"), Occurs.Never(),
            "the drain must have been dropped deliberately, not thrown out of the build step -- otherwise this test would pass for the wrong reason");
    }

    [Test]
    public void Repeated_trips_without_success_follow_the_full_backoff_sequence_and_clamp_at_the_cap()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);
        ArrangeReadableBatch();

        Action probe = null;
        var delays = new List<TimeSpan>();
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => { probe = action; delays.Add(delay); });
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        // No intervening success: the first trip needs the 2-failure grace (starting from _backoffIndex
        // == 0); every retrip after that needs only 1, since we're already in the failing regime (each
        // round's probe is invoked to resume sampling -- EndBackoffProbe deliberately leaves the index
        // alone, so the next round's trip continues the escalation instead of restarting it).
        service.DrainOnce();
        service.DrainOnce();
        probe.Invoke();

        for (var i = 0; i < 6; i++)
        {
            service.DrainOnce();
            probe.Invoke();
        }

        Assert.That(delays, Is.EqualTo(new[]
        {
            TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(300),
            TimeSpan.FromSeconds(300),
        }));
    }

    [Test]
    public void A_single_failure_after_a_probe_retrips_immediately_without_a_fresh_grace_period()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);
        ArrangeReadableBatch();

        Action probe = null;
        var delays = new List<TimeSpan>();
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => { probe = action; delays.Add(delay); });
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // first trip needs the 2-failure grace
        probe.Invoke();

        service.DrainOnce(); // a single failure here must retrip immediately -- no fresh grace

        Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15) }));
    }

    [Test]
    public void Disabling_and_re_enabling_while_backing_off_clears_the_stuck_gate()
    {
        // Regression test: before the fix, a probe firing while disabled left _sendBackoffActive true
        // forever -- StartLocked had nothing to clear it, so re-enabling never resumed drains.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);
        ArrangeReadableBatch();

        Action probe = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probe = action);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff, schedules a probe

        var disabled = Mock.Create<IConfiguration>();
        Mock.Arrange(() => disabled.ContinuousProfilingEnabled).Returns(false);
        service.OverrideConfigForTesting(disabled);
        service.ApplyConfigChange();
        Assert.That(service.IsActive, Is.False);

        // The pending probe fires while disabled -- must not resurrect anything.
        probe.Invoke();
        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Once(), "the probe must not resume native sampling while disabled");

        EnableAndStart(service);
        Assert.That(service.IsActive, Is.True);

        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();

        // 2 failures before disable + 1 recovery send after re-enable: without the fix, this last drain
        // would still be gated and Send would never reach a 3rd call.
        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(3));
    }

    [Test]
    public void EndBackoffProbe_resumes_native_sampling_at_the_active_interval_and_clears_the_gate()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service, 12345);
        ArrangeReadableBatch();

        Action probe = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probe = action);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce();

        probe.Invoke();

        Mock.Assert(() => _native.Start(12345), Occurs.Exactly(2), "once from EnableAndStart, once from the probe resume");

        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();

        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(3), "the gate must be clear for this drain to reach Send");
    }

    [Test]
    public void A_backoff_probe_whose_native_resume_throws_reschedules_itself_instead_of_wedging_CP()
    {
        // Regression test: EndBackoffProbeIfCurrent is the ONLY path that clears _sendBackoffActive. If the
        // resume's _native.Start throws (a transient P/Invoke blip), an escaping exception would leave the
        // backoff gate stuck true forever with no further probe scheduled -- native stopped, every drain a
        // no-op, IsActive false until a config change. The probe must instead catch, reschedule a fresh
        // probe under the same generation, and recover once the transient failure clears.
        var (service, transport) = NewConnectedService();
        using var _ = service;

        // The resume's native start throws once (transient), then succeeds. Arranged BEFORE EnableAndStart so
        // its own initial start is counted too, and count invocations here rather than via Mock.Assert:
        // JustMock does not register a call whose DoInstead throws, so the throwing resume would be invisible
        // to an Occurs assertion.
        var resumeShouldThrow = false;
        var startCalls = 0;
        Mock.Arrange(() => _native.Start(Arg.IsAny<int>()))
            .DoInstead(() => { startCalls++; if (resumeShouldThrow) throw new InvalidOperationException("transient P/Invoke failure"); });

        EnableAndStart(service, 12345);
        ArrangeReadableBatch();

        var probes = new List<Action>();
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probes.Add(action));
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff, schedules probe[0]
        Assert.That(probes, Has.Count.EqualTo(1), "the trip must schedule the first probe");

        // The probe fires but the native resume throws: it must NOT wedge -- a fresh probe is rescheduled and
        // the backoff gate stays armed.
        resumeShouldThrow = true;
        probes[0].Invoke();

        Assert.That(probes, Has.Count.EqualTo(2), "a failed resume must reschedule a fresh probe rather than abandoning recovery");
        Assert.That(service.IsActive, Is.False, "the backoff gate must stay armed after a failed resume, not clear on a throw");

        // The transient failure clears; firing the rescheduled probe resumes sampling and clears the gate.
        resumeShouldThrow = false;
        probes[1].Invoke();

        Assert.That(service.IsActive, Is.True, "CP must recover once the transient resume failure clears");
        Assert.That(startCalls, Is.EqualTo(3), "EnableAndStart + the throwing resume + the successful retry resume");

        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();
        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(3), "the gate must be clear for this drain to reach Send after recovery");
    }

    [Test]
    public void IsActive_reports_false_while_backing_off_and_true_again_once_resumed()
    {
        // IsActive is consumed by ThreadProfilingService's mutual-exclusion guard (L6): while CP is
        // paused-and-probing after repeated send failures, native sampling is stopped, so IsActive
        // must report false so a start_profiler command isn't refused for the whole backoff window.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);
        ArrangeReadableBatch();

        Action probe = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probe = action);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        Assert.That(service.IsActive, Is.True);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff, schedules a probe

        Assert.That(service.IsActive, Is.False, "backing off must report inactive to the thread-profiling guard");

        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        probe.Invoke(); // resumes sampling

        Assert.That(service.IsActive, Is.True, "resuming after the probe must report active again");
    }

    [Test]
    public void A_probe_firing_after_dispose_does_not_resurrect_native_sampling()
    {
        // Regression test: AgentManager disposes the CP service before the container-owned Scheduler, so a
        // pending probe can fire after Dispose. It must see the post-Dispose state and stay stopped.
        var (service, transport) = NewConnectedService();
        EnableAndStart(service);
        ArrangeReadableBatch();

        Action probe = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probe = action);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips, schedules the probe

        service.Dispose();

        probe.Invoke();

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Once(), "only the original EnableAndStart call; the post-Dispose probe must not resume sampling");
    }

    [Test]
    public void ApplyConfigChange_after_Dispose_does_not_resurrect_the_session()
    {
        var (service, _) = NewConnectedService();
        EnableAndStart(service);

        service.Dispose();

        // Simulate a deferred ApplyConfigChange landing after Dispose (e.g. the 15s thread-profiling
        // deferral, or a config-update event queued just before shutdown).
        service.ApplyConfigChange();

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Once(), "only from EnableAndStart; the post-Dispose config change must not restart native sampling");
        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Once(), "the drain schedule must not be re-armed after Dispose");
        Assert.That(service.IsActive, Is.False);
        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.False, "the wrapper hot-path seam must stay disarmed after Dispose");
    }

    [Test]
    public void A_reconnect_while_backing_off_resumes_immediately_and_fully_resets_state()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);
        ArrangeReadableBatch();

        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);
        service.DrainOnce();
        service.DrainOnce(); // trips backoff

        // A reconnect arrives (e.g. a new redirect host) while still waiting out the backoff delay.
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.eu01.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        Mock.Assert(() => transport.UpdateEndpoint("https://collector.eu01.nr-data.net/v1/profiles"), Occurs.Once());

        // Resumed immediately -- no need to wait for the scheduled probe.
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();

        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(3), "2 failures + 1 recovery send, not gated waiting for the probe");
    }

    [Test]
    public void A_reconnect_racing_a_backoff_trip_cannot_leave_native_stopped_with_a_cleared_gate()
    {
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);
        ArrangeReadableBatch();

        // Trip backoff normally (2 failures).
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);
        service.DrainOnce();
        service.DrainOnce();
        Mock.Assert(() => _native.Stop(), Occurs.Once());

        // A reconnect arrives "concurrently" -- with the fix, this can only run before or after the
        // trip completes under the same lock, never in the middle of it.
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        // Whatever order the lock serialized these in, native must be running again (resumed by the
        // reconnect, since it's the last state-changing event) -- not stopped with a cleared gate. Exactly
        // 2, not AtLeastOnce: EnableAndStart already called Start once, so AtLeastOnce would pass on the
        // fixture's own setup call even if the reconnect never resumed anything.
        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Exactly(2), "once from EnableAndStart, once from the reconnect's resume");

        // And the drain path must still be live: a subsequent failure must be able to re-trip.
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);
        service.DrainOnce();
        service.DrainOnce();
        Mock.Assert(() => _native.Stop(), Occurs.Exactly(2)); // the original trip + this new one
    }

    [Test]
    public void A_reconnect_landing_mid_send_does_not_corrupt_backoff_state()
    {
        // Regression test for the "underlying (rare) non-atomic-counter race" flagged but not fixed
        // during the 2026-07-27 opus backoff review (#6): a reconnect (-> ResumeAfterReconnect, under
        // _lifecycleLock) landing on another thread while THIS drain's Send() is still executing (Send
        // runs deliberately outside _lifecycleLock -- OnSendResult only takes it after Send returns) is
        // the tightest realistic interleaving between the two. Whichever order the lock serializes them
        // in, _consecutiveSendFailures/_backoffIndex/_sendBackoffActive must end up coherent: no exception,
        // no out-of-range SendBackoffSequence index, and the drain path must still be able to re-trip
        // afterward -- proving the state was not left corrupted or stuck.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);
        ArrangeReadableBatch();

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);

        // A failing send whose completion races a reconnect landing before this drain's OnSendResult
        // acquires _lifecycleLock -- simulating the reconnect's scheduler thread interleaving between
        // Send() returning and OnSendResult running, which is exactly the window Send()'s deliberate
        // placement outside the lock leaves open.
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()))
            .Returns(() =>
            {
                EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });
                return false;
            });

        Assert.DoesNotThrow(() =>
        {
            service.DrainOnce();
            service.DrainOnce();
        }, "a reconnect racing OnSendResult must never corrupt backoff state or throw");

        // The drain path must still be live and able to re-trip -- proof the interleaving above left
        // _consecutiveSendFailures/_backoffIndex in a valid, usable state rather than stuck or corrupted.
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);
        Assert.DoesNotThrow(() =>
        {
            service.DrainOnce();
            service.DrainOnce();
        }, "the drain path must still be able to re-trip after the raced interleaving");
    }

    [Test]
    public void A_stale_probe_from_a_superseded_backoff_round_does_not_collapse_a_later_round()
    {
        // Regression test for the stale-probe bug: IScheduler can't cancel a pending one-shot, so a probe
        // from round 1 stays scheduled after a reconnect ends round 1 early. When a later round 2 trips and
        // that stale probe finally fires, it must NOT resume sampling / clear the backoff gate for round 2.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service);
        ArrangeReadableBatch();

        var probes = new List<Action>();
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probes.Add(action));
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        // Round 1 trips (2 failures) and schedules probe A.
        service.DrainOnce();
        service.DrainOnce();
        Assert.That(probes, Has.Count.EqualTo(1));

        // A reconnect resumes sampling early, ending round 1 -- probe A stays pending (it can't be cancelled).
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        // Sends keep failing -> round 2 trips (grace reset by the reconnect) and schedules probe B.
        service.DrainOnce();
        service.DrainOnce();
        Assert.That(probes, Has.Count.EqualTo(2));

        // Start count so far: EnableAndStart (1) + the reconnect's resume (2). Round 2's trip re-stopped it.
        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Exactly(2));

        // The stale probe A fires in the middle of round 2. It must be a no-op.
        probes[0].Invoke();
        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Exactly(2), "the stale probe must not resume native sampling");

        // Round 2's backoff gate must still be closed: a drain stays gated, so no further send happens.
        service.DrainOnce();
        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(4), "the stale probe must not have reopened the backoff gate");

        // The current probe B does resume sampling.
        probes[1].Invoke();
        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Exactly(3), "the current round's probe resumes sampling");
    }

    [Test]
    public void EndBackoffProbe_deferred_behind_thread_profiling_does_not_resume_until_TP_finishes()
    {
        // Regression test for H1 (2026-08-31 review): before the fix, a backoff probe resumed native
        // sampling unconditionally, walking straight through the mutual-exclusion handshake if a
        // thread-profiling session started while CP was backing off (IsActive reports false during
        // backoff, so ThreadProfilingService's forward guard admits the session).
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service, 12345);
        ArrangeReadableBatch();

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(true);
        service.ThreadProfilingStatus = tpStatus;

        Action probe = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => probe = action);
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff, schedules the original probe

        var originalProbe = probe;
        probe = null;
        originalProbe.Invoke(); // fires while a thread-profiling session is active

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Once(), "the probe must not resume native sampling while thread profiling is active");
        Assert.That(service.IsActive, Is.False, "backoff must stay armed while the resume is deferred behind thread profiling");
        Assert.That(probe, Is.Not.Null, "a retry must be scheduled instead of silently dropping the resume");

        // Thread profiling finishes; firing the rescheduled retry (not the stale original) resumes sampling.
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(false);
        probe.Invoke();

        Mock.Assert(() => _native.Start(12345), Occurs.Exactly(2), "once from EnableAndStart, once from the deferred retry's resume");
        Assert.That(service.IsActive, Is.True);

        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();

        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(3), "the gate must be clear for this drain to reach Send");
    }

    [Test]
    public void ResumeAfterReconnect_deferred_behind_thread_profiling_preserves_backoff_state_until_TP_finishes()
    {
        // Same H1 handshake, via the reconnect path instead of the backoff probe.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service, 12345);
        ArrangeReadableBatch();

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(true);
        service.ThreadProfilingStatus = tpStatus;

        // Discard the original TripBackoffAndScheduleProbeLocked probe; only the reconnect's own
        // deferred retry (scheduled below) matters to this test.
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()));
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);

        Action retry = null;
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => retry = action);

        // A reconnect arrives while thread profiling is active -- must not resume sampling.
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Once(), "the reconnect must not resume native sampling while thread profiling is active");
        Assert.That(service.IsActive, Is.False, "backoff must stay armed while the reconnect's resume is deferred");
        Assert.That(retry, Is.Not.Null, "a retry of the reconnect resume must be scheduled");

        // Thread profiling finishes; firing the deferred retry resumes sampling and fully resets
        // backoff state, exactly as an immediate (non-deferred) reconnect resume would have.
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(false);
        retry.Invoke();

        Mock.Assert(() => _native.Start(12345), Occurs.Exactly(2), "once from EnableAndStart, once from the deferred retry's resume");
        Assert.That(service.IsActive, Is.True);

        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);
        service.DrainOnce();

        Mock.Assert(() => transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Exactly(3), "2 failures + 1 recovery send, gate must be clear after the deferred resume");
    }

    [Test]
    public void A_deferred_reconnect_resume_does_not_restart_sampling_the_rounds_own_probe_already_resumed()
    {
        // The reconnect resume's gate check happens in OnAgentConnected, lock-free and (on the deferred
        // path below) minutes before the resume itself actually runs. By then the backoff round it meant to
        // end early can already have been ended by its own probe -- native sampling is running again. A
        // resume must therefore re-check the gate under the lock: starting an already-running sampler also
        // resets _lastDrainTimestamp a second time, understating the duration window of the next profile.
        var (service, transport) = NewConnectedService();
        using var _ = service;
        EnableAndStart(service, 12345);
        ArrangeReadableBatch();

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(false);
        service.ThreadProfilingStatus = tpStatus;

        var scheduled = new List<Action>();
        Mock.Arrange(() => _scheduler.ExecuteOnce(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>()))
            .DoInstead((Action action, TimeSpan delay) => scheduled.Add(action));
        Mock.Arrange(() => transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(false);

        service.DrainOnce();
        service.DrainOnce(); // trips backoff and schedules this round's probe
        Assert.That(scheduled, Has.Count.EqualTo(1));

        // A reconnect arrives while a thread-profiling session is in flight, so its resume defers and
        // reschedules itself -- deliberately leaving the round, and the round's own probe, untouched.
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(true);
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });
        Assert.That(scheduled, Has.Count.EqualTo(2), "the reconnect's resume must have deferred behind thread profiling and rescheduled itself");

        // Thread profiling finishes and the round's own probe wins the race: sampling resumes, gate clears.
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(false);
        scheduled[0].Invoke();
        Mock.Assert(() => _native.Start(12345), Occurs.Exactly(2), "once from EnableAndStart, once from the probe's resume");

        // The deferred reconnect retry now lands on an already-resumed session.
        scheduled[1].Invoke();

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Exactly(2),
            "the reconnect resume must not start a sampler the round's probe already restarted");

        // And nothing is wedged: the drain path is live and can still trip a fresh round.
        service.DrainOnce();
        Mock.Assert(() => _native.Stop(), Occurs.Exactly(2), "the original trip plus a fresh one -- the drain path must still be live");
    }

    #endregion

    #region Task-3-format batch builder (mirrors BufferParserTests)

    private const byte StartBatch = 0x01, StartSample = 0x02, EndBatch = 0x06;

    private static void WriteShort(MemoryStream s, short v) { s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    private static void WriteLong(MemoryStream s, long v) { for (var i = 7; i >= 0; i--) s.WriteByte((byte)(v >> (i * 8))); }
    private static void WriteString(MemoryStream s, string v)
    {
        var bytes = Encoding.Unicode.GetBytes(v); // UTF-16LE
        WriteShort(s, (short)v.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static byte[] OneSampleBatch(string thread, long osId, long tHigh, long tLow, long span, string[] framesLeafFirst)
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

    #endregion
}
