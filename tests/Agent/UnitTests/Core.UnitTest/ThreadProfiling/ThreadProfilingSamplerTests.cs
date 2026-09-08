// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.ThreadProfiling;

[TestFixture]
public class ThreadProfilingSamplerTests
{
    private INativeMethods _nativeMethods;
    private ThreadProfilingSampler _threadProfiler;
    private ISampleSink _sampleSink;
    private ILogger _nrLogger;

    [SetUp]
    public void Setup()
    {
        _nativeMethods = Mock.Create<INativeMethods>();
        _sampleSink = Mock.Create<ISampleSink>();
        _threadProfiler = new ThreadProfilingSampler(_nativeMethods);

        _nrLogger = Mock.Create<ILogger>();
        Mock.Arrange(() => _nrLogger.IsWarnEnabled).Returns(true);
        Log.Initialize(_nrLogger);
    }

    [TearDown]
    public void TearDown()
    {
        Log.Initialize(new NoOpLogger());
    }

    [Test]
    public void Start_WhenCalled_ShouldStartWorkerThread()
    {
        // Arrange
        uint frequencyInMsec = 1000;
        uint durationInMsec = 1000;

        // Act
        var result = _threadProfiler.Start(frequencyInMsec, durationInMsec, _sampleSink, _nativeMethods);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task FullCycleTest()
    {
        // Arrange
        int length = 1;

        int countofSnapshots = 1;

        var snapshots = Marshal.AllocHGlobal(
            UIntPtr.Size + // thread id
            sizeof(int) + //HRESULT
            sizeof(int) + // count of snapshots
            IntPtr.Size // pointer to an array of functionIds
        );

        var functionIds = Marshal.AllocHGlobal(UIntPtr.Size * countofSnapshots);
        Marshal.WriteInt64(functionIds, 456);

        var marshaledFakeIntPtr = snapshots;
        Marshal.WriteInt64(marshaledFakeIntPtr, 123); // threadId
        marshaledFakeIntPtr += UIntPtr.Size;
        Marshal.WriteInt32(marshaledFakeIntPtr, 1); // hresult
        marshaledFakeIntPtr += sizeof(int);
        Marshal.WriteInt32(marshaledFakeIntPtr, countofSnapshots); // count of snapshots
        marshaledFakeIntPtr += sizeof(int);
        Marshal.WriteIntPtr(marshaledFakeIntPtr, functionIds); // pointer to array of function ids

        Mock.Arrange(() => _nativeMethods.RequestProfile(out snapshots, out length)).Returns(1);

        Mock.Arrange(() => _nativeMethods.ShutdownNativeThreadProfiler()).OccursOnce();

        uint frequencyInMsec = 100;
        uint durationInMsec = 1000;

        // Act
        _threadProfiler.Start(frequencyInMsec, durationInMsec, _sampleSink, _nativeMethods);
        await Task.Delay(1500); // wait for the profiler to capture something, then stop it
        _threadProfiler.Stop();

        // Assert
        Mock.Assert(_nativeMethods);

        Marshal.FreeHGlobal(snapshots);
        Marshal.FreeHGlobal(functionIds);
    }

    [Test]
    public async Task InternalPolling_WaitCallback_HandlesException()
    {
        // Arrange
        uint frequencyInMsec = 250;
        uint durationInMsec = 1000;

        int length = 0;
        IntPtr snapshots = IntPtr.Zero;
        Mock.Arrange(() => _nativeMethods.RequestProfile(out snapshots, out length))
            .Throws(new Exception("Kaboom!"))
            .OccursAtLeast(1); // may happen multiple times because of the frequency vs duration setting

        // Act
        var result = _threadProfiler.Start(frequencyInMsec, durationInMsec, _sampleSink, _nativeMethods);
        await Task.Delay(1500); // give the callback time to do it's bit
        _threadProfiler.Stop();

        // Assert
        Mock.Assert(_nativeMethods);
    }


    [Test]
    public void Start_WhenWorkerIsAlreadyRunning_ShouldNotStartAnotherWorker()
    {
        // Arrange
        uint frequencyInMsec = 1000;
        uint durationInMsec = 1000;

        // Start the first worker
        _threadProfiler.Start(frequencyInMsec, durationInMsec, _sampleSink, _nativeMethods);

        // Act
        var result = _threadProfiler.Start(frequencyInMsec, durationInMsec, _sampleSink, _nativeMethods);

        // Assert
        Assert.That(result, Is.False); // Assert that a second worker wasn't started
    }

    [Test]
    public void Stop_WhenNoWorkerIsRunning_ShouldDoNothing()
    {
        // Arrange
        // Here we're not starting a worker, so there's no worker running

        // Act
        _threadProfiler.Stop();

        // Assert
        Mock.Assert(() => _nativeMethods.ShutdownNativeThreadProfiler(), Occurs.Never());
    }

    [Test]
    public void Stop_WhenWorkerIsRunning_SignalsAndJoinsWorker()
    {
        // Arrange: a long duration so the worker won't self-terminate on its own,
        // with a short sampling frequency so it's parked in its wait loop. That way
        // it is Stop()'s shutdown signal -- not the elapsed duration -- that ends it.
        uint frequencyInMsec = 50;
        uint durationInMsec = 600000; // 10 minutes; far longer than the test could ever wait

        Mock.Arrange(() => _nativeMethods.ShutdownNativeThreadProfiler()).OccursOnce();

        _threadProfiler.Start(frequencyInMsec, durationInMsec, _sampleSink, _nativeMethods);

        // Make sure the worker is actually up before we ask it to stop.
        var started = SpinWait.SpinUntil(() => _threadProfiler.IsRunning, TimeSpan.FromSeconds(5));
        Assert.That(started, Is.True, "worker thread did not start running");

        // Act
        _threadProfiler.Stop();

        // Assert: Stop() signals shutdown and joins the worker, so by the time it
        // returns the worker has fully wound down -- IsRunning is false and the native
        // profiler was torn down -- long before the configured 10-minute duration. On
        // the pre-fix inverted condition Stop() would no-op, leaving IsRunning true and
        // ShutdownNativeThreadProfiler uncalled.
        Assert.That(_threadProfiler.IsRunning, Is.False);
        Mock.Assert(_nativeMethods);
    }

    [Test]
    public void Stop_WhenWorkerDoesNotExitPromptly_ReturnsAtJoinTimeoutAndLogsWarning()
    {
        // Arrange: block the worker inside its native call so it can't observe the shutdown signal
        // until the block is released -- simulating a stack walk that outlives Stop()'s patience.
        // Use a short join timeout so the test doesn't have to wait out the real 5-second default.
        // A hand-written fake (not a JustMock mock) is used here because the fake's RequestProfile
        // blocks a real background thread inside the call -- JustMock's interception isn't safe to
        // invoke concurrently from another thread while the main thread is still using the same mock.
        var joinTimeout = TimeSpan.FromMilliseconds(200);
        var nativeMethods = new BlockingNativeMethods();
        var threadProfiler = new ThreadProfilingSampler(nativeMethods, joinTimeout);

        uint frequencyInMsec = 10;
        uint durationInMsec = 600000; // long enough that only Stop() can end the run

        threadProfiler.Start(frequencyInMsec, durationInMsec, _sampleSink, nativeMethods);

        // Wait until the worker is actually blocked inside the native call -- IsRunning alone flips true
        // the instant Start() returns, before the worker thread has run at all, which races Stop()'s
        // shutdown signal ahead of the worker ever reaching the call it's meant to be stuck in.
        var entered = nativeMethods.EnteredRequestProfile.Wait(TimeSpan.FromSeconds(5));
        Assert.That(entered, Is.True, "worker thread never reached the native call");

        try
        {
            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            threadProfiler.Stop();
            stopwatch.Stop();

            // Assert: Stop() gives up waiting at the bounded timeout instead of hanging on the
            // still-blocked worker, and it still leaves the worker running (native teardown never ran).
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
            Assert.That(threadProfiler.IsRunning, Is.True);
            Mock.Assert(() => _nrLogger.Warn(Arg.IsAny<string>(), Arg.IsAny<object[]>()), Occurs.Once());
        }
        finally
        {
            // Let the blocked worker unwind so it doesn't outlive the test.
            nativeMethods.ReleaseRequestProfile.Set();
        }
    }

    private class BlockingNativeMethods : INativeMethods
    {
        public readonly ManualResetEventSlim EnteredRequestProfile = new ManualResetEventSlim(false);
        public readonly ManualResetEventSlim ReleaseRequestProfile = new ManualResetEventSlim(false);

        public int RequestProfile(out IntPtr snapshots, out int length)
        {
            snapshots = IntPtr.Zero;
            length = 0;
            EnteredRequestProfile.Set();
            ReleaseRequestProfile.Wait();
            return 1;
        }

        public void ReleaseProfile() { }
        public int RequestFunctionNames(UIntPtr[] functionIds, int length, out IntPtr functionInfo) { functionInfo = IntPtr.Zero; return 0; }
        public void ShutdownNativeThreadProfiler() { }
        public int InstrumentationRefresh() => 0;
        public int ReloadConfiguration() => 0;
        public int AddCustomInstrumentation(string fileName, string xml) => 0;
        public int ApplyCustomInstrumentation() => 0;
        public void ContinuousProfilerStart(int intervalMs) { }
        public void ContinuousProfilerStop() { }
        public int ContinuousProfilerReadThreadSamples(int len, byte[] buffer) => 0;
        public void ContinuousProfilerSetTraceContext(long traceIdHigh, long traceIdLow, long spanId) { }
        public void ContinuousProfilerResetTraceContext() { }
        public void ContinuousProfilerSetAgentWork() { }
        public void ContinuousProfilerResetAgentWork() { }
        public void ContinuousProfilerShutdown() { }
    }
}