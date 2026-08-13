// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.DataTransport;
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
        // Reset the process-wide seam so one test's enabled context can't leak into another.
        ContinuousProfilingContext.Instance = new ContinuousProfilingContext();
    }

    private void ArrangeEnabled(int intervalMs = 10000)
    {
        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _config.ContinuousProfilingSamplingIntervalMs).Returns(intervalMs);
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        _service.OverrideConfigForTesting(_config);
    }

    [Test]
    public void Enabling_via_config_starts_the_drain_schedule()
    {
        ArrangeEnabled(10000);

        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(10000), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void StartIfEnabled_when_disabled_does_not_schedule()
    {
        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(_config);

        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()), Occurs.Never());
        Assert.That(_service.IsActive, Is.False);
    }

    [Test]
    public void StartIfEnabled_when_already_active_does_not_reschedule()
    {
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        _service.StartIfEnabled();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()), Occurs.Once());
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

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()), Occurs.Once());
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
        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(20000), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
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
    public void ApplyConfigChange_enabling_from_disabled_starts()
    {
        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(false);
        _service.OverrideConfigForTesting(_config);
        _service.StartIfEnabled();
        Assert.That(_service.IsActive, Is.False);

        ArrangeEnabled(10000);
        _service.ApplyConfigChange();

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(10000), Arg.IsAny<TimeSpan?>()), Occurs.Once());
        Assert.That(_service.IsActive, Is.True);
    }

    [Test]
    public void Drain_tick_with_no_data_does_not_send()
    {
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

        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Drain", Arg.IsAny<long>()), Occurs.AtLeast(1));
        Mock.Assert(() => _health.ReportSupportabilityCountMetric("Supportability/DotNET/ContinuousProfiling/Samples", Arg.IsAny<long>()), Occurs.AtLeast(1));
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
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) => dest.Length + 1);

        Assert.DoesNotThrow(() => _service.DrainOnce());
        Mock.Assert(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>()), Occurs.Never());
    }

    [Test]
    public void Drain_tick_never_throws_when_source_throws()
    {
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
    public void StartIfEnabled_defers_when_thread_profiling_active()
    {
        ArrangeEnabled(10000);

        var tpStatus = Mock.Create<IThreadProfilingStatus>();
        Mock.Arrange(() => tpStatus.IsThreadProfilingActive).Returns(true);
        _service.ThreadProfilingStatus = tpStatus;

        _service.StartIfEnabled();

        // Deferred: no recurring drain scheduled, not active, but a retry was scheduled.
        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()), Occurs.Never());
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

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(10000), Arg.IsAny<TimeSpan?>()), Occurs.Once());
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

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()), Occurs.Never());
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

        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), TimeSpan.FromMilliseconds(10000), Arg.IsAny<TimeSpan?>()), Occurs.Once());
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
        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()), Occurs.Once(), "the drain schedule must not be re-armed after Dispose");
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
