// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Threading;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class ThreadEventsListenerTests
    {
        [SetUp]
        public void SetUp()
        {
            ThreadEventsListener.EventSourceIdToMonitor = TestEventSource.TestEventSourceGuid;
        }

        [TearDown]
        public void TearDown()
        {
            ThreadEventsListener.EventSourceIdToMonitor = ThreadEventsListener.ClrEventSourceId;
        }

        [Test]
        public void ShouldAccumulateThreadpoolThroughputStatsWithRealEventSource()
        {
            using (var threadEventListener = CreateThreadEventsListenerForRealEventSource())
            {
                QueueUpWorkAndWaitForCompletion();
                QueueUpWorkAndWaitForCompletion();
                QueueUpWorkAndWaitForCompletion();

                var sample = threadEventListener.Sample();

                NrAssert.Multiple(
                    () => ClassicAssert.GreaterOrEqual(3, sample.CountThreadRequestsQueued),
                    () => ClassicAssert.GreaterOrEqual(3, sample.CountThreadRequestsDequeued),
                    () => ClassicAssert.GreaterOrEqual(0, sample.ThreadRequestQueueLength)
                );
            }
        }

        [Test]
        public void ShouldAccumulateThreadpoolThroughputStats()
        {
            using (var eventSource = new TestEventSource())
            using (var threadEventListener = CreateThreadEventsListener())
            {
                eventSource.EnqueueThread();
                eventSource.EnqueueThread();
                eventSource.EnqueueThread();

                eventSource.DequeueThread();
                eventSource.DequeueThread();

                var sample = threadEventListener.Sample();

                NrAssert.Multiple(
                    () => ClassicAssert.AreEqual(3, sample.CountThreadRequestsQueued),
                    () => ClassicAssert.AreEqual(2, sample.CountThreadRequestsDequeued),
                    () => ClassicAssert.AreEqual(1, sample.ThreadRequestQueueLength)
                );
            }
        }

        [Test]
        public void ShouldResetThreadpoolThroughputStatsWhenSampled()
        {
            using (var eventSource = new TestEventSource())
            using (var threadEventListener = CreateThreadEventsListener())
            {
                eventSource.EnqueueThread();
                eventSource.EnqueueThread();
                eventSource.EnqueueThread();

                eventSource.DequeueThread();
                eventSource.DequeueThread();

                threadEventListener.Sample(); //This call should reset the accumulated stats

                var sample = threadEventListener.Sample();

                NrAssert.Multiple(
                    () => ClassicAssert.AreEqual(0, sample.CountThreadRequestsQueued),
                    () => ClassicAssert.AreEqual(0, sample.CountThreadRequestsDequeued),
                    () => ClassicAssert.AreEqual(1, sample.ThreadRequestQueueLength) //Queue length does not reset
                );
            }
        }

        [Test]
        public void ThreadpoolThroughputStatsShouldNotReportNegativeQueueLengths()
        {
            using (var eventSource = new TestEventSource())
            using (var threadEventListener = CreateThreadEventsListener())
            {
                eventSource.DequeueThread();
                eventSource.DequeueThread();
                eventSource.DequeueThread();
                eventSource.DequeueThread();

                var sample = threadEventListener.Sample();

                NrAssert.Multiple(
                    () => ClassicAssert.AreEqual(0, sample.CountThreadRequestsQueued),
                    () => ClassicAssert.AreEqual(4, sample.CountThreadRequestsDequeued),
                    () => ClassicAssert.AreEqual(0, sample.ThreadRequestQueueLength)
                );
            }
        }

        [Test]
        public void ShouldResetThreadpoolThroughputQueueLengthWhenSampled()
        {
            using (var eventSource = new TestEventSource())
            using (var threadEventListener = CreateThreadEventsListener())
            {
                eventSource.DequeueThread();
                eventSource.DequeueThread();
                eventSource.DequeueThread();
                eventSource.DequeueThread();

                threadEventListener.Sample(); //This call should reset the accumulated stats

                eventSource.EnqueueThread();
                eventSource.EnqueueThread();
                eventSource.EnqueueThread();
                eventSource.EnqueueThread();
                eventSource.EnqueueThread();

                var sample = threadEventListener.Sample();

                NrAssert.Multiple(
                    () => ClassicAssert.AreEqual(5, sample.CountThreadRequestsQueued),
                    () => ClassicAssert.AreEqual(0, sample.CountThreadRequestsDequeued),
                    () => ClassicAssert.AreEqual(5, sample.ThreadRequestQueueLength) //Would be 1 if the queueLength was not reset
                );
            }
        }

        private ISampledEventListener<ThreadpoolThroughputEventsSample> CreateThreadEventsListener()
        {
            return new ThreadEventsListener();
        }

        private ISampledEventListener<ThreadpoolThroughputEventsSample> CreateThreadEventsListenerForRealEventSource()
        {
            ThreadEventsListener.EventSourceIdToMonitor = ThreadEventsListener.ClrEventSourceId;
            return CreateThreadEventsListener();
        }

        private void QueueUpWorkAndWaitForCompletion()
        {
            using (var manualResetEvent = new ManualResetEventSlim())
            {

                ThreadPool.QueueUserWorkItem(DoWork);

                manualResetEvent.Wait();

                void DoWork(object _)
                {
                    manualResetEvent.Set();
                }
            }
        }

        [EventSource(Guid = "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
        private class TestEventSource : EventSource
        {
            public static readonly Guid TestEventSourceGuid = Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");

            [Event(ThreadEventsListener.EventId_ThreadPoolEnqueue, Level = EventLevel.Verbose)]
            public void EnqueueThread()
            {
                WriteEvent(ThreadEventsListener.EventId_ThreadPoolEnqueue);
            }

            [Event(ThreadEventsListener.EventId_ThreadPoolDequeue, Level = EventLevel.Verbose)]
            public void DequeueThread()
            {
                WriteEvent(ThreadEventsListener.EventId_ThreadPoolDequeue);
            }
        }
    }
}
