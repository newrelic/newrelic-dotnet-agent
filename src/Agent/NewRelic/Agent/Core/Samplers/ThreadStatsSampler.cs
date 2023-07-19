// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.SystemInterfaces;
using System.Threading;
using NewRelic.Core.Logging;
using System;
using System.Diagnostics.Tracing;

namespace NewRelic.Agent.Core.Samplers
{
    public class ThreadStatsSampler : AbstractSampler
    {
        public ISampledEventListener<ThreadpoolThroughputEventsSample> _listener;
        private readonly Func<ISampledEventListener<ThreadpoolThroughputEventsSample>> _threadListenerFactory;
        private readonly IThreadStatsSampleTransformer _transformer;
        private const int ThreadStatsSampleIntervalSeconds = 60;
        private readonly IThreadPoolStatic _threadPoolProxy;

        public ThreadStatsSampler(IScheduler scheduler, Func<ISampledEventListener<ThreadpoolThroughputEventsSample>> threadEventListenerFactory, IThreadStatsSampleTransformer threadpoolStatsTransformer, IThreadPoolStatic threadpoolProxy)
         : base(scheduler, TimeSpan.FromSeconds(ThreadStatsSampleIntervalSeconds))
        {
            _threadPoolProxy = threadpoolProxy;
            _threadListenerFactory = threadEventListenerFactory;

            _transformer = threadpoolStatsTransformer;
        }

        public override void Sample()
        {
            if (_listener == null)
            {
                return;
            }

            try
            {
                _threadPoolProxy.GetMaxThreads(out int countWorkerThreadsMax, out int countCompletionThreadsMax);
                _threadPoolProxy.GetAvailableThreads(out int countWorkerThreadsAvail, out int countCompletionThreadsAvail);

                var stats = new ThreadpoolUsageStatsSample(countWorkerThreadsMax, countWorkerThreadsAvail, countCompletionThreadsMax, countCompletionThreadsAvail);

                _transformer.Transform(stats);

                var sample = _listener.Sample();
                _transformer.Transform(sample);
            }
            catch (Exception ex)
            {
                Log.Error($"Unable to get Threadpool stats sample.  No .Net Threadpool metrics will be reported.  Error : {ex}");
                Dispose();
            }
        }

        public override void Start()
        {
            base.Start();

            if (!Enabled)
            {
                return;
            }

            if ((_configuration?.EventListenerSamplersEnabled).GetValueOrDefault(false))
            {
                _listener = _listener ?? _threadListenerFactory();
            }
        }

        protected override void Stop()
        {
            base.Stop();
            _listener?.StopListening();
        }

        public override void Dispose()
        {
            base.Dispose();
            _listener?.StopListening();
#if NETFRAMEWORK // calling .Dispose() in .NET 7 explodes. No idea why.
                _listener?.Dispose();
#endif
            _listener = null;
        }
    }

    public class ThreadEventsListener : SampledEventListener<ThreadpoolThroughputEventsSample>
    {
        public static readonly Guid ClrEventSourceId = Guid.Parse("8e9f5090-2d75-4d03-8a81-e5afbf85daf1");
        public static Guid EventSourceIdToMonitor = ClrEventSourceId; //We can't rely on the .ctor to initialize this because OnEventSourceCreated is called in the base .ctor before we have a chance to execute our .ctor
        public const int EventId_ThreadPoolDequeue = 31;
        public const int EventId_ThreadPoolEnqueue = 30;

        private const int _threadpool_Keyword = 0x0002;
        private int _countThreadRequestsQueued;
        private int _countThreadRequestsDequeued;

        //volatile to allow reads on a separate thread from writes
        private volatile int _threadRequestQueueLength;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Guid == EventSourceIdToMonitor)
            {
                _eventSource = eventSource;
                StartListening();
                base.OnEventSourceCreated(eventSource);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            switch (eventData.EventId)
            {
                case EventId_ThreadPoolEnqueue:
                    Interlocked.Increment(ref _countThreadRequestsQueued);
                    Interlocked.Increment(ref _threadRequestQueueLength);
                    break;

                case EventId_ThreadPoolDequeue:
                    Interlocked.Increment(ref _countThreadRequestsDequeued);
                    Interlocked.Decrement(ref _threadRequestQueueLength);
                    break;
            }
        }

        public override ThreadpoolThroughputEventsSample Sample()
        {
            //There is a small chance that the queue length can go below 0.
            //This occurs when the the listener starts up after some thread requests
            //have been queued but have not started.  This will cause an imbalance
            //where the the queuelength will decrement, but it would not have incremented 
            //as the item was queued (b/c we weren't listening yet).
            //The following logic will correct this.  It is here instead of in the
            //OnEventWritten to mitigate performance.
            if (_threadRequestQueueLength < 0)
            {
                Interlocked.Exchange(ref _threadRequestQueueLength, 0);
            }

            var result = new ThreadpoolThroughputEventsSample(
                Interlocked.Exchange(ref _countThreadRequestsQueued, 0),
                Interlocked.Exchange(ref _countThreadRequestsDequeued, 0),
                _threadRequestQueueLength);

            return result;
        }

        public override void StartListening()
        {
            if (_eventSource != null)
            {
                EnableEvents(_eventSource, EventLevel.LogAlways, (EventKeywords)_threadpool_Keyword);
            }
        }
    }

    public class ThreadpoolUsageStatsSample
    {
        public readonly int WorkerCountThreadsAvail;
        public readonly int WorkerCountThreadsUsed;
        public readonly int CompletionCountThreadsAvail;
        public readonly int CompletionCountThreadsUsed;

        public ThreadpoolUsageStatsSample(int countWorkerThreadsMax, int countWorkerThreadAvail, int countCompletionThreadssMax, int countCompletionThreadsAvail)
        {
            WorkerCountThreadsUsed = countWorkerThreadsMax - countWorkerThreadAvail;
            WorkerCountThreadsAvail = countWorkerThreadAvail;

            CompletionCountThreadsUsed = countCompletionThreadssMax - countCompletionThreadsAvail;
            CompletionCountThreadsAvail = countCompletionThreadsAvail;
        }
    }

    public class ThreadpoolThroughputEventsSample
    {
        public readonly int CountThreadRequestsQueued;
        public readonly int CountThreadRequestsDequeued;
        public readonly int ThreadRequestQueueLength;

        public ThreadpoolThroughputEventsSample(int countThreadRequestsQueued, int countThreadRequestsDequeued, int threadRequestQueueLength)
        {
            CountThreadRequestsQueued = countThreadRequestsQueued;

            CountThreadRequestsDequeued = countThreadRequestsDequeued;

            //The sampler corrects this condition as quickly as possible, but there is a small chance that this
            //can still be sent as a negative.  This prevents us from sending up bad/invalid/unclear data
            //to APM.
            ThreadRequestQueueLength = threadRequestQueueLength < 0
                ? 0
                : threadRequestQueueLength;
        }
    }

    public enum ThreadType
    {
        Worker,
        Completion
    }

    public enum ThreadStatus
    {
        Available,
        InUse
    }

    public enum ThreadpoolThroughputStatsType
    {
        Requested,
        Started,
        QueueLength
    }
}
