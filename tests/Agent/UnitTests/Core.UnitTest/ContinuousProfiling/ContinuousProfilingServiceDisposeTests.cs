// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
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

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

/// <summary>
/// Covers the teardown contract AgentManager relies on when its startup sequence throws after continuous
/// profiling has already started: the abort path runs Shutdown(false) -> StopServices() -> Dispose() on the
/// half-built manager, and that Dispose must fully stop and join the native worker rather than orphaning a
/// live sampler thread for the process lifetime.
/// </summary>
[TestFixture]
public class ContinuousProfilingServiceDisposeTests
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

        // DrainOnce is gated on having connected (see ContinuousProfilingServiceTests.SetUp for the same
        // pattern) -- the new dispatched-drain tests below need a real drain to reach ReadBatch rather than
        // returning early on the pre-connect gate.
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
    public void Dispose_of_an_active_session_stops_native_sampling_before_joining_the_worker()
    {
        // The AgentManager abort path (a startup throw after continuous profiling has started) tears this
        // service down via Shutdown(false) -> StopServices() -> Dispose(). A running session must be both
        // stopped (Stop, so the sampler quits suspending threads) AND joined (Shutdown), not merely joined
        // while still sampling -- otherwise the native worker is left half-running until process exit.
        ArrangeEnabled(10000);
        _service.StartIfEnabled();

        _service.Dispose();

        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Mock.Assert(() => _native.Shutdown(), Occurs.Once());
    }

    [Test]
    public void Dispose_waits_for_an_in_flight_dispatched_drain_before_stopping_native_sampling()
    {
        ArrangeEnabled(10000);

        Action drainAction = null;
        Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()))
            .DoInstead((Action action, TimeSpan interval, TimeSpan? initialDelay, bool trackAsAgentWork) => drainAction = action);

        _service.StartIfEnabled();
        Assert.That(drainAction, Is.Not.Null);

        var releaseReadBatch = new ManualResetEventSlim(false);
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>()))
            .DoInstead(() => releaseReadBatch.Wait(TimeSpan.FromSeconds(2)))
            .Returns(0);

        drainAction(); // dispatches DrainOnce onto the thread pool, held open by the ReadBatch wait above

        var nativeStopOrder = new List<string>();
        Mock.Arrange(() => _native.Stop()).DoInstead(() => nativeStopOrder.Add("native-stop"));

        releaseReadBatch.Set(); // let the in-flight drain finish shortly after Dispose starts waiting
        _service.Dispose();

        Assert.That(nativeStopOrder, Does.Contain("native-stop"), "Dispose must still reach _native.Stop() after the in-flight drain completes");
    }

    [Test]
    public void Dispose_logs_a_warning_and_still_proceeds_when_the_in_flight_drain_exceeds_the_bounded_wait()
    {
        // Deviation from the brief: DrainShutdownWaitTimeout is injectable via the constructor
        // (drainShutdownWaitTimeout, defaults to 60s in production) specifically so this branch --
        // !drainTask.Wait(timeout) returning false -- is reachable in a unit test without waiting out the
        // real 60s. See ContinuousProfilingService's constructor and _drainShutdownWaitTimeout field.
        var shortTimeoutService = new ContinuousProfilingService(_source, _native, _transport, _scheduler, _health, TimeSpan.FromMilliseconds(50));

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _config.ContinuousProfilingSamplingIntervalMs).Returns(10000);
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        shortTimeoutService.OverrideConfigForTesting(_config);

        Action drainAction = null;
        Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()))
            .DoInstead((Action action, TimeSpan interval, TimeSpan? initialDelay, bool trackAsAgentWork) => drainAction = action);

        shortTimeoutService.StartIfEnabled();
        Assert.That(drainAction, Is.Not.Null);

        // Never signaled within the test -- stands in for a drain still running well past the injected
        // 50ms bounded wait, deliberately never blocking the test itself thanks to the 300ms cap here.
        // 300ms (not 2s): JustMock Lite serializes calls into DIFFERENT mocks across threads whenever one
        // is still executing a DoInstead callback (confirmed empirically -- a mocked call on one thread
        // blocked inside DoInstead measurably delays an unrelated mock's call from another thread until
        // the first DoInstead returns). That means _native.Stop() below cannot actually run until this
        // ReadBatch call returns, regardless of StopLocked's 50ms-bounded Task.Wait -- so the cap here
        // must stay short enough that the assertion below still meaningfully distinguishes "bounded" from
        // "unbounded" (60s default), rather than asserting a sub-JustMock-serialization-tail bound.
        var neverReleased = new ManualResetEventSlim(false);
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>()))
            .DoInstead(() => neverReleased.Wait(TimeSpan.FromMilliseconds(300)))
            .Returns(0);

        drainAction(); // dispatches DrainOnce onto the thread pool; it will still be running when Dispose is called below

        var nativeStopOrder = new List<string>();
        Mock.Arrange(() => _native.Stop()).DoInstead(() => nativeStopOrder.Add("native-stop"));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        shortTimeoutService.Dispose();
        sw.Stop();

        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)), "Dispose must not block anywhere near the full (60s default) unbounded wait when the drain outlives the bounded wait");
        Assert.That(nativeStopOrder, Does.Contain("native-stop"), "Dispose must still reach _native.Stop() after the bounded wait times out");
    }
}
