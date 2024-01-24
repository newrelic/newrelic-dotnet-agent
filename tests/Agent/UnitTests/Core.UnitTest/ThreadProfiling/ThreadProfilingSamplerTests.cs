// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;
using Telerik.JustMock;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    [TestFixture]
    public class ThreadProfilingSamplerTests
    {
        private INativeMethods _nativeMethods;
        private ThreadProfilingSampler _threadProfiler;
        private ISampleSink _sampleSink;

        [SetUp]
        public void Setup()
        {
            _nativeMethods = Mock.Create<INativeMethods>();
            _sampleSink = Mock.Create<ISampleSink>();
            _threadProfiler = new ThreadProfilingSampler(_nativeMethods);
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
    }
}
