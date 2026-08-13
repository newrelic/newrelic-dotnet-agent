// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Time;
using NUnit.Framework;
using Telerik.JustMock;

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
        _service = new ContinuousProfilingService(_source, _native, _transport, _scheduler, _health);
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

        Mock.Assert(() => _transport.Send(Arg.IsAny<global::OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest>()), Occurs.Never());
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

        Mock.Assert(() => _transport.Send(Arg.IsAny<global::OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest>()), Occurs.Once());
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

        Mock.Assert(() => _health.ReportSupportabilityCountMetric(Arg.IsAny<string>(), Arg.IsAny<long>()), Occurs.AtLeast(1));
    }

    [Test]
    public void Drain_tick_with_bytesRead_exceeding_buffer_length_is_discarded()
    {
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) => dest.Length + 1);

        Assert.DoesNotThrow(() => _service.DrainOnce());
        Mock.Assert(() => _transport.Send(Arg.IsAny<global::OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest>()), Occurs.Never());
    }

    [Test]
    public void Drain_tick_never_throws_when_source_throws()
    {
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _service.DrainOnce());
        Mock.Assert(() => _transport.Send(Arg.IsAny<global::OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest>()), Occurs.Never());
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
        Mock.Arrange(() => _transport.Send(Arg.IsAny<global::OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest>()))
            .Throws(new InvalidOperationException("send failed"));

        Assert.DoesNotThrow(() => _service.DrainOnce());
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
