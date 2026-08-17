// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
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
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using Telerik.JustMock;
using ExportProfilesRequest = OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest;

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
    private ILogger _nrLogger;

    [SetUp]
    public void SetUp()
    {
        _source = Mock.Create<ISampleSource>();
        _native = Mock.Create<INativeContinuousProfiler>();
        _transport = Mock.Create<IProfilesTransport>();
        _scheduler = Mock.Create<IScheduler>();
        _health = Mock.Create<IAgentHealthReporter>();
        _config = Mock.Create<IConfiguration>();

        // Send now returns bool; default to "accepted" so tests that reach a real send (the two
        // dispatched-drain-racing-Stop tests below) don't trip the send-failure backoff by accident.
        Mock.Arrange(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(true);

        // Swap in a mock ILogger so the bounded-wait timeout test below can assert on the Warn call
        // StopLocked makes when drainTask.Wait(...) actually times out -- a direct behavioral proof that
        // the wait is bounded, rather than an elapsed-time assertion (see that test's comments for why
        // wall-clock timing alone is not a reliable discriminator here).
        _nrLogger = Mock.Create<ILogger>();
        Log.Initialize(_nrLogger);

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
        Log.Initialize(new NoOpLogger());
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
        //
        // Uses a REAL blocking ISampleSource stub (BlockingSampleSource below), NOT a JustMock mock, for the
        // in-flight drain. This is load-bearing: JustMock Lite serializes calls into DIFFERENT mocks across
        // threads for the entire duration of a DoInstead callback (confirmed empirically -- while one thread
        // is blocked inside a mock's DoInstead, another thread's UNRELATED mock call blocks until that
        // DoInstead returns). If the drain were held in-flight by blocking inside a mocked ReadBatch's
        // DoInstead, StopLocked's own _scheduler.StopExecuting(...) mock call -- which runs BEFORE the bounded
        // wait -- would block behind it until the drain released, by which point the drain Task has completed
        // and StopLocked skips the wait entirely (drainTask.IsCompleted == true): no timeout, no Warn, so the
        // test could never observe the branch it exists to prove. Blocking inside a plain (non-mock) object
        // holds no JustMock lock, so StopExecuting proceeds immediately and the drain stays genuinely
        // in-flight across StopLocked's 50ms bounded wait, which then actually times out and logs the Warn.
        var drainReachedReadBatch = new ManualResetEventSlim(false);
        var releaseReadBatch = new ManualResetEventSlim(false);
        var blockingSource = new BlockingSampleSource(drainReachedReadBatch, releaseReadBatch);
        var shortTimeoutService = new ContinuousProfilingService(blockingSource, _native, _transport, _scheduler, _health, TimeSpan.FromMilliseconds(50));

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

        var nativeStopOrder = new List<string>();
        Mock.Arrange(() => _native.Stop()).DoInstead(() => nativeStopOrder.Add("native-stop"));

        drainAction(); // dispatches DrainOnce onto the thread pool; it blocks in the stub's ReadBatch below

        // Wait until the drain is genuinely in-flight (past DrainOnce's _disposed guard, blocked inside
        // ReadBatch) before calling Dispose. Without this barrier the test races the thread pool: a slow
        // pool start would let Dispose set _disposed first, DrainOnce would return at its opening guard, the
        // drain Task would complete immediately, and StopLocked would skip its bounded wait (nothing to wait
        // for) -- no timeout, no Warn -- for a reason unrelated to what this test proves. Mirrors the
        // sendStarted.Wait() barrier the send-step test below uses.
        Assert.That(drainReachedReadBatch.Wait(TimeSpan.FromSeconds(5)), Is.True, "the dispatched drain must actually reach ReadBatch (be in-flight) before Dispose runs");

        shortTimeoutService.Dispose();

        // Let the still-blocked drain unwind now that the assertions' precondition (it outlived the bounded
        // wait) has been met -- avoids leaving a pool thread parked in ReadBatch after the test returns.
        releaseReadBatch.Set();

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("Timed out")), Arg.IsAny<object[]>()), Occurs.Once(),
            "StopLocked must have logged the timeout warning -- proof drainTask.Wait(...) actually returned false (timed out) rather than blocking unboundedly until the drain finished");
        Assert.That(nativeStopOrder, Does.Contain("native-stop"), "Dispose must still reach _native.Stop() after the bounded wait times out");
    }

    [Test]
    public void A_burst_of_ticks_that_lose_the_in_flight_guard_does_not_clobber_the_handle_to_the_real_drain()
    {
        // Regression test for the _lastDrainTask staleness finding: _drainAction used to publish a task
        // into _lastDrainTask on EVERY tick, including ticks that lose DrainOnce's _drainInFlight guard and
        // return almost instantly as a no-op. Because the recurring timer keeps firing regardless of how
        // long the real drain takes, a burst of such skipped ticks racing a slow real drain would overwrite
        // _lastDrainTask with an already-completed skip-task, making StopLocked's bounded wait below think
        // there was nothing to wait for (drainTask.IsCompleted == true) -- no wait, no timeout, no Warn, and
        // _native.Stop()/Shutdown() would tear down native sampling while the real drain was still running
        // detached. The fix: only DrainOnce (never _drainAction) writes _lastDrainTask, and only after it
        // has actually won the _drainInFlight guard -- a losing tick returns before ever touching the field.
        //
        // Same BlockingSampleSource + short-timeout-service technique as the test above (needed so the real
        // drain stays genuinely in-flight, unserialized by a JustMock DoInstead lock, across the bounded wait).
        var drainReachedReadBatch = new ManualResetEventSlim(false);
        var releaseReadBatch = new ManualResetEventSlim(false);
        var blockingSource = new BlockingSampleSource(drainReachedReadBatch, releaseReadBatch);
        var shortTimeoutService = new ContinuousProfilingService(blockingSource, _native, _transport, _scheduler, _health, TimeSpan.FromMilliseconds(50));

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

        var nativeStopOrder = new List<string>();
        Mock.Arrange(() => _native.Stop()).DoInstead(() => nativeStopOrder.Add("native-stop"));

        drainAction(); // real drain: dispatches DrainOnce, which wins the guard and blocks in ReadBatch below

        Assert.That(drainReachedReadBatch.Wait(TimeSpan.FromSeconds(5)), Is.True, "the real dispatched drain must actually reach ReadBatch (be in-flight) before the burst of skipped ticks below");

        // Simulate the recurring timer firing several more times while the real drain is still blocked in
        // ReadBatch -- every one of these loses the _drainInFlight guard inside DrainOnce and must return
        // without touching _lastDrainTask. Give each a moment to actually run and return on the thread pool.
        for (var i = 0; i < 10; i++)
        {
            drainAction();
        }
        Thread.Sleep(TimeSpan.FromMilliseconds(200));

        shortTimeoutService.Dispose();

        releaseReadBatch.Set(); // let the real drain unwind now that the assertions' precondition is met

        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("Timed out")), Arg.IsAny<object[]>()), Occurs.Once(),
            "StopLocked must still have waited on (and timed out on) the REAL drain -- if the burst of skipped ticks had clobbered _lastDrainTask with a completed skip-task, this wait (and its Warn) would have been silently skipped entirely");
        Assert.That(nativeStopOrder, Does.Contain("native-stop"), "Dispose must still reach _native.Stop() after the bounded wait times out");
    }

    /// <summary>
    /// A real (non-mock) <see cref="ISampleSource"/> whose ReadBatch signals that the drain has entered it
    /// and then blocks until released, returning 0 (no batch) so DrainOnce returns right after. Deliberately
    /// not a JustMock mock: see the timeout test's comment for why blocking here (rather than in a mocked
    /// ReadBatch's DoInstead) is what lets StopLocked's pre-wait _scheduler.StopExecuting mock call proceed
    /// instead of serializing behind the in-flight drain.
    /// </summary>
    private sealed class BlockingSampleSource : ISampleSource
    {
        private readonly ManualResetEventSlim _entered;
        private readonly ManualResetEventSlim _release;

        public BlockingSampleSource(ManualResetEventSlim entered, ManualResetEventSlim release)
        {
            _entered = entered;
            _release = release;
        }

        public int ReadBatch(byte[] destination)
        {
            _entered.Set();
            // Cap the block so a test that forgets to release can never hang the run indefinitely; the cap is
            // far longer than the 50ms bounded wait under test so the drain reliably outlives it.
            _release.Wait(TimeSpan.FromSeconds(10));
            return 0;
        }
    }

    [Test]
    public void Dispose_does_not_stall_for_the_full_bounded_wait_when_a_real_drain_races_it_at_the_send_step()
    {
        // Regression test for the lock-ordering-inversion finding: StopLocked calls drainTask.Wait(...)
        // WHILE HOLDING _lifecycleLock. Before the fix, a drain that had already read a real batch and
        // reached OnSendResult (which unconditionally took _lifecycleLock) would race Dispose/StopLocked
        // and stall its bounded wait for the FULL DrainShutdownWaitTimeout, every time -- defeating the
        // point of a bounded wait entirely. Every other test in this file/ContinuousProfilingServiceTests
        // arranges ReadBatch to return 0, which makes DrainOnce return before ever reaching OnSendResult --
        // exactly why the existing suite stayed green despite the bug. This test arranges a REAL batch and
        // a slow Send so the drain actually reaches OnSendResult while Dispose is concurrently blocked in
        // StopLocked's wait, and proves Dispose returns promptly rather than burning the full timeout.
        ArrangeEnabled(10000);

        Action drainAction = null;
        Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()))
            .DoInstead((Action action, TimeSpan interval, TimeSpan? initialDelay, bool trackAsAgentWork) => drainAction = action);

        _service.StartIfEnabled();
        Assert.That(drainAction, Is.Not.Null);

        var batch = OneSampleBatch("worker-1", 1, 0, 0, 0, new[] { "F()" });
        Mock.Arrange(() => _source.ReadBatch(Arg.IsAny<byte[]>())).Returns((byte[] dest) =>
        {
            Array.Copy(batch, dest, batch.Length);
            return batch.Length;
        });

        var sendStarted = new ManualResetEventSlim(false);
        var releaseSend = new ManualResetEventSlim(false);
        Mock.Arrange(() => _transport.Send(Arg.IsAny<ExportProfilesRequest>())).Returns(() =>
        {
            sendStarted.Set();
            releaseSend.Wait(TimeSpan.FromSeconds(5));
            return true;
        });

        drainAction(); // dispatches the real drain onto the thread pool
        Assert.That(sendStarted.Wait(TimeSpan.FromSeconds(5)), Is.True, "the dispatched drain must actually reach Send");

        // Dispose on its own thread: it will enter StopLocked, take _lifecycleLock, and block in
        // drainTask.Wait(...) since the drain above hasn't returned from Send yet. A dedicated thread rather than
        // Task.Run because it begins executing immediately: a queued thread-pool item may not start within the
        // 200ms positioning window below on a constrained runner, in which case the drain's OnSendResult would run
        // to completion before Dispose ever entered StopLocked -- the interleaving under test never happens and the
        // pre-fix production code would pass too, making this a silent false negative that still shows green.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var dispose = new Thread(() => _service.Dispose()) { IsBackground = true };
        dispose.Start();

        // Give Dispose time to actually enter the bounded wait before releasing Send -- this is what
        // positions the drain's later OnSendResult call to race StopLocked's wait rather than run to
        // completion beforehand, i.e. the exact interleaving the fix targets.
        Thread.Sleep(TimeSpan.FromMilliseconds(200));
        releaseSend.Set();

        Assert.That(dispose.Join(TimeSpan.FromSeconds(5)), Is.True, "Dispose must complete promptly once the racing drain's send finishes");
        sw.Stop();

        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)),
            "Dispose must not stall for anywhere near the full (60s default) DrainShutdownWaitTimeout just because a real drain raced it at the send/OnSendResult step");
        Mock.Assert(() => _native.Stop(), Occurs.Once());
        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("Timed out")), Arg.IsAny<object[]>()), Occurs.Never(),
            "this drain finishes well within the bound -- StopLocked must not have timed out waiting for it");
    }

    [Test]
    public void A_second_concurrent_stop_waits_for_the_first_to_finish_and_does_not_stop_native_twice()
    {
        // Regression test for the narrower race the deadlock fix reopens. StopLocked temporarily DROPS
        // _lifecycleLock during its bounded drain wait (to avoid the OnSendResult self-deadlock). That opens
        // a window in which a SECOND thread can acquire _lifecycleLock and enter StopLocked while the first
        // stop is still mid-flight (native.Stop() not yet called, _isActive still true). Without the
        // _stopInProgress guard, that second caller would race the first -- calling _native.Stop() a second
        // time and/or proceeding to _native.Shutdown() before the first stop's native.Stop() had run. The
        // guard makes the second caller wait for the first stop's real completion. This test drives exactly
        // that interleaving deterministically and asserts _native.Stop() runs exactly once.
        //
        // Real (non-mock) blocking source, same reason as the timeout test above: the in-flight drain must
        // stay in-flight WITHOUT holding JustMock's per-DoInstead cross-thread lock, or the first stopper's
        // own pre-wait mock calls would serialize behind it and collapse the window.
        var drainReachedReadBatch = new ManualResetEventSlim(false);
        var releaseReadBatch = new ManualResetEventSlim(false);
        var blockingSource = new BlockingSampleSource(drainReachedReadBatch, releaseReadBatch);
        var service = new ContinuousProfilingService(blockingSource, _native, _transport, _scheduler, _health, TimeSpan.FromSeconds(30));

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _config.ContinuousProfilingSamplingIntervalMs).Returns(10000);
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        service.OverrideConfigForTesting(_config);

        Action drainAction = null;
        Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()))
            .DoInstead((Action action, TimeSpan interval, TimeSpan? initialDelay, bool trackAsAgentWork) => drainAction = action);

        // Fires from inside the FIRST stopper's StopLocked, after it has published _stopInProgress and is
        // about to drop _lifecycleLock for its bounded wait -- StopExecuting is called there while the lock
        // is still held, so once this signals, the first stopper is committed and the second caller entering
        // StopLocked is guaranteed to observe _stopInProgress != null.
        var firstStopperInStopLocked = new ManualResetEventSlim(false);
        Mock.Arrange(() => _scheduler.StopExecuting(Arg.IsAny<Action>()))
            .DoInstead(() => firstStopperInStopLocked.Set());

        var nativeStopCount = 0;
        Mock.Arrange(() => _native.Stop()).DoInstead(() => Interlocked.Increment(ref nativeStopCount));

        service.StartIfEnabled();
        Assert.That(drainAction, Is.Not.Null);

        drainAction(); // dispatch the drain; it blocks in the stub's ReadBatch, keeping itself in-flight
        Assert.That(drainReachedReadBatch.Wait(TimeSpan.FromSeconds(5)), Is.True, "the drain must be in-flight before either stop runs");

        // First stopper: enters StopLocked, publishes _stopInProgress, then drops _lifecycleLock and blocks
        // in its bounded wait for the still-in-flight drain.
        // Dedicated threads rather than Task.Run for both stoppers: a raw thread begins executing immediately,
        // whereas a queued thread-pool work item can sit unstarted past the 200ms positioning window below on a
        // busy/constrained runner (pool thread-injection delay). If the second stopper never entered StopLocked
        // inside that window the race would simply not happen -- and the pre-fix production code would pass too,
        // making this test a silent false negative that still shows green.
        var firstStop = new Thread(() => service.Dispose()) { IsBackground = true };
        firstStop.Start();
        Assert.That(firstStopperInStopLocked.Wait(TimeSpan.FromSeconds(5)), Is.True, "the first stopper must reach StopLocked");

        // Second stopper: acquires _lifecycleLock the moment the first drops it at its wait, enters
        // StopLocked, sees _stopInProgress != null, and waits for the first stop to genuinely finish.
        var secondStop = new Thread(() => service.Dispose()) { IsBackground = true };
        secondStop.Start();

        // Give the second stopper time to enter its _stopInProgress wait BEFORE the first stop completes --
        // release too early and the first stop would clear _stopInProgress before the second observed it,
        // sending the second down the ordinary path and calling _native.Stop() twice. Same 200ms positioning
        // heuristic the send-step test uses.
        Thread.Sleep(TimeSpan.FromMilliseconds(200));
        releaseReadBatch.Set();

        Assert.That(firstStop.Join(TimeSpan.FromSeconds(10)) && secondStop.Join(TimeSpan.FromSeconds(10)), Is.True,
            "both concurrent stops must complete without deadlocking");
        Assert.That(nativeStopCount, Is.EqualTo(1), "_native.Stop() must run exactly once across two concurrent stops -- the second must defer to the first, not race it");
    }

    [Test]
    public void A_start_after_dispose_does_not_arm_a_session()
    {
        // StartLocked's own _disposed guard, exercised through its simplest reachable caller: nothing else on
        // the StartIfEnabled path checks _disposed, so before the guard this armed a fresh session (native
        // Start + drain timer + trace-context seam) on a service whose native worker Dispose had already
        // joined. The interleaving test below covers the concurrent form the drop-and-reacquire window opens.
        ArrangeEnabled(10000);

        _service.Dispose();

        _service.StartIfEnabled();

        Mock.Assert(() => _native.Start(Arg.IsAny<int>()), Occurs.Never());
        Mock.Assert(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()), Occurs.Never());
        Assert.That(_service.IsActive, Is.False, "a disposed service must never report an active session");
        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.False, "the trace-context seam must stay disarmed after dispose");
    }

    [Test]
    public void A_retunes_start_that_resumes_after_a_racing_dispose_does_not_arm_a_new_session()
    {
        // Regression test for the window StopLocked's Monitor.Exit/Enter drop-and-reacquire opens for
        // StartLocked. A retune runs `lock (_lifecycleLock) { StopLocked(); StartLocked(); }`; StopLocked drops
        // the lock for its bounded drain wait, so Dispose can acquire it in that window, set _disposed, park on
        // _stopInProgress, and let the retune finish -- after which the retune's StartLocked runs and, without
        // the _disposed guard, arms native sampling and a drain timer that Dispose's immediately-following
        // _native.Shutdown() invalidates. Not a crash (Shutdown is idempotent) but a permanently stuck session:
        // _isActive true forever, blocking thread profiling, with a live drain timer and an armed trace-context
        // seam pushing into a shut-down profiler.
        //
        // Real (non-mock) blocking source, same reason as the tests above: an in-flight drain held inside a
        // mocked ReadBatch's DoInstead would serialize the stopper's own pre-wait mock calls behind it and
        // collapse the window this test needs.
        var drainReachedReadBatch = new ManualResetEventSlim(false);
        var releaseReadBatch = new ManualResetEventSlim(false);
        var blockingSource = new BlockingSampleSource(drainReachedReadBatch, releaseReadBatch);
        var intervalMs = 10000;
        var service = CreateConnectedService(blockingSource, () => intervalMs);

        Action drainAction = null;
        var executeEveryCount = 0;
        Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()))
            .DoInstead((Action action, TimeSpan interval, TimeSpan? initialDelay, bool trackAsAgentWork) =>
            {
                drainAction = action;
                Interlocked.Increment(ref executeEveryCount);
            });

        // Fires from inside the retune's StopLocked, after it published _stopInProgress and while it still holds
        // _lifecycleLock -- so once this signals, Dispose is guaranteed to observe _stopInProgress != null.
        var retuneReachedStopLocked = new ManualResetEventSlim(false);
        Mock.Arrange(() => _scheduler.StopExecuting(Arg.IsAny<Action>()))
            .DoInstead(() => retuneReachedStopLocked.Set());

        var nativeStartCount = 0;
        Mock.Arrange(() => _native.Start(Arg.IsAny<int>())).DoInstead(() => Interlocked.Increment(ref nativeStartCount));

        service.StartIfEnabled();
        Assert.That(drainAction, Is.Not.Null);
        Assert.That(nativeStartCount, Is.EqualTo(1), "precondition: the first session started");

        drainAction(); // dispatch the drain; it blocks in the stub's ReadBatch, keeping itself in-flight
        Assert.That(drainReachedReadBatch.Wait(TimeSpan.FromSeconds(5)), Is.True, "the drain must be in-flight before the retune runs");

        intervalMs = 20000; // an interval change is what makes ApplyConfigChange a StopLocked-then-StartLocked retune
        // Dedicated threads rather than Task.Run, same reason as the two-stoppers test above: a queued thread-pool
        // item may not start executing within the 200ms positioning window on a constrained runner, in which case
        // the interleaving under test never occurs and the test passes vacuously. A raw thread starts immediately.
        var retune = new Thread(() => service.ApplyConfigChange()) { IsBackground = true };
        retune.Start();
        Assert.That(retuneReachedStopLocked.Wait(TimeSpan.FromSeconds(5)), Is.True, "the retune must reach StopLocked");

        var dispose = new Thread(() => service.Dispose()) { IsBackground = true };
        dispose.Start();

        // Let Dispose acquire the lock the retune dropped at its bounded wait, set _disposed, and park on
        // _stopInProgress BEFORE the retune's stop can complete. Releasing earlier would let the retune finish
        // its stop and its StartLocked before Dispose ever ran, which is not the interleaving under test. Same
        // 200ms positioning heuristic the two tests above use.
        Thread.Sleep(TimeSpan.FromMilliseconds(200));
        releaseReadBatch.Set();

        Assert.That(retune.Join(TimeSpan.FromSeconds(10)) && dispose.Join(TimeSpan.FromSeconds(10)), Is.True,
            "the retune and the racing dispose must both complete without deadlocking");

        Assert.That(nativeStartCount, Is.EqualTo(1),
            "the retune's StartLocked resumed after Dispose set _disposed -- it must not have started native sampling again");
        Assert.That(executeEveryCount, Is.EqualTo(1), "no new drain timer may be armed after dispose");
        Assert.That(service.IsActive, Is.False, "_isActive must not be left stuck true, which would block thread profiling for the process lifetime");
        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.False, "the trace-context seam must not be left armed against a shut-down profiler");
    }

    [Test]
    public void A_second_stop_whose_wait_is_followed_by_a_start_still_stops_native()
    {
        // The complement of A_second_concurrent_stop_waits_for_the_first_to_finish_and_does_not_stop_native_twice:
        // deferring to an in-flight stop is only correct while that stop leaves the session stopped. Here the
        // first stopper is a RETUNE, which holds _lifecycleLock across `StopLocked(); StartLocked();` -- so by
        // the time the second caller's wait on _stopInProgress returns and it can reacquire the lock, _isActive
        // is true again. Returning at that point (the original behavior) silently swallowed the second caller's
        // own stop request: StopFromCommand/ApplyConfigChange's disable branch would report a stopped session
        // while native sampling ran on until the next lifecycle transition. It must instead re-check _isActive
        // and perform the stop itself.
        var drainReachedReadBatch = new ManualResetEventSlim(false);
        var releaseReadBatch = new ManualResetEventSlim(false);
        var blockingSource = new BlockingSampleSource(drainReachedReadBatch, releaseReadBatch);
        var intervalMs = 10000;
        var service = CreateConnectedService(blockingSource, () => intervalMs);

        Action drainAction = null;
        Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>(), Arg.IsAny<bool>()))
            .DoInstead((Action action, TimeSpan interval, TimeSpan? initialDelay, bool trackAsAgentWork) => drainAction = action);

        var retuneReachedStopLocked = new ManualResetEventSlim(false);
        Mock.Arrange(() => _scheduler.StopExecuting(Arg.IsAny<Action>()))
            .DoInstead(() => retuneReachedStopLocked.Set());

        var nativeStopCount = 0;
        Mock.Arrange(() => _native.Stop()).DoInstead(() => Interlocked.Increment(ref nativeStopCount));

        service.StartIfEnabled();
        Assert.That(drainAction, Is.Not.Null);

        drainAction(); // dispatch the drain; it blocks in the stub's ReadBatch, keeping itself in-flight
        Assert.That(drainReachedReadBatch.Wait(TimeSpan.FromSeconds(5)), Is.True, "the drain must be in-flight before either stop runs");

        intervalMs = 20000; // retune: StopLocked (drops the lock at its bounded wait) then StartLocked
        // Dedicated threads rather than Task.Run, same reason as the two tests above: a queued thread-pool item may
        // not start executing within the 200ms positioning window on a constrained runner, which would skip the
        // interleaving under test entirely and pass vacuously. A raw thread starts immediately.
        var retune = new Thread(() => service.ApplyConfigChange()) { IsBackground = true };
        retune.Start();
        Assert.That(retuneReachedStopLocked.Wait(TimeSpan.FromSeconds(5)), Is.True, "the retune must reach StopLocked");

        // Second caller with its own genuine stop intent, landing in the retune's dropped-lock window: it sees
        // _stopInProgress != null and waits for the retune's stop to finish.
        var commandStop = new Thread(() => service.StopFromCommand(new[] { ContinuousProfilingCommandTypes.Cpu })) { IsBackground = true };
        commandStop.Start();

        Thread.Sleep(TimeSpan.FromMilliseconds(200));
        releaseReadBatch.Set();

        Assert.That(retune.Join(TimeSpan.FromSeconds(10)) && commandStop.Join(TimeSpan.FromSeconds(10)), Is.True,
            "the retune and the concurrent command stop must both complete without deadlocking");

        Assert.That(nativeStopCount, Is.EqualTo(2),
            "the command stop resumed to find the retune had restarted the session -- it must have stopped native sampling itself instead of returning");
        Assert.That(service.IsActive, Is.False, "the command stop's intent must be reflected in the session state");
        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("Timed out")), Arg.IsAny<object[]>()), Occurs.Never(),
            "neither bounded wait should have timed out in this interleaving");
    }

    /// <summary>
    /// Builds a service on the shared mocks with a caller-supplied sample source and a live sampling-interval
    /// accessor (so a test can flip the interval to make ApplyConfigChange a retune), publishes an
    /// AgentConnectedEvent so drains do real work, and installs the shared config mock.
    /// </summary>
    private ContinuousProfilingService CreateConnectedService(ISampleSource source, Func<int> samplingIntervalMs)
    {
        var service = new ContinuousProfilingService(source, _native, _transport, _scheduler, _health, TimeSpan.FromSeconds(30));

        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => connectionInfo.Host).Returns("collector.nr-data.net");
        Mock.Arrange(() => connectionInfo.Port).Returns(443);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo });

        Mock.Arrange(() => _config.ContinuousProfilingEnabled).Returns(true);
        Mock.Arrange(() => _config.ContinuousProfilingSamplingIntervalMs).Returns(samplingIntervalMs);
        Mock.Arrange(() => _config.ApplicationNames).Returns(new[] { "MyApp" });
        service.OverrideConfigForTesting(_config);

        return service;
    }

    private const byte StartBatch = 0x01, StartSample = 0x02, EndBatch = 0x06;

    private static void WriteShort(MemoryStream s, short v) { s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    private static void WriteLong(MemoryStream s, long v) { for (var i = 7; i >= 0; i--) s.WriteByte((byte)(v >> (i * 8))); }
    private static void WriteString(MemoryStream s, string v)
    {
        var bytes = Encoding.Unicode.GetBytes(v); // UTF-16LE
        WriteShort(s, (short)v.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    // Mirrors ContinuousProfilingServiceTests.OneSampleBatch -- duplicated locally rather than shared
    // because the two test classes are otherwise independent fixtures with their own SetUp/mocks.
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
}
