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
        private ManualResetEventSlim _shutdownEvent;

        [SetUp]
        public void Setup()
        {
            _nativeMethods = Mock.Create<INativeMethods>();
            _sampleSink = Mock.Create<ISampleSink>();
            _shutdownEvent = Mock.Create<ManualResetEventSlim>();
            _threadProfiler = new ThreadProfilingSampler(_nativeMethods, _shutdownEvent);
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
            Assert.IsTrue(result);
        }

        [Test]
        public async Task Stop_WhenCalled_ShouldStopWorkerThread()
        {
            // Arrange
            int length;
            IntPtr snapshots;

            Mock.Arrange(() => _nativeMethods.RequestProfile(out snapshots, out length))
                .DoInstead(() =>
                    {
                        snapshots =  Marshal.AllocHGlobal(
                            UIntPtr.Size  + // thread id
                            Marshal.SizeOf(typeof(int)) + //HRESULT
                            sizeof(int) + // count of snapshots

                            );
                        length = 1;

                    })
                .Returns(1);

            Mock.Arrange(() => _nativeMethods.ShutdownNativeThreadProfiler()).OccursOnce();

            uint frequencyInMsec = 100;
            uint durationInMsec = 1000;

            // Start the worker
            _threadProfiler.Start(frequencyInMsec, durationInMsec, _sampleSink, _nativeMethods);

            // Act
            await Task.Delay(5000); // wait for the 

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
            Assert.IsFalse(result); // Assert that a second worker wasn't started
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
