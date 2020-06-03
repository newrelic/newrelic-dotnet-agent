using CompositeTests;
using NBench;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Diagnostics;

namespace Benchmarking
{
    public class TransactionTransformerBenchmarking
    {
        private const string TransformCounterName = "TransformCounter";
        private static readonly TimeSpan PerIterationRunTime = TimeSpan.FromMinutes(1);

        private Counter _counter;
        private CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;

        [PerfSetup]
        public void Setup(BenchmarkContext context)
        {
            _counter = context.GetCounter(TransformCounterName);
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();

            CreateTransactionAndSegments();
        }

        [PerfBenchmark(
            Description = "Throughput test for TransactionTransformer.Transform",
            RunMode = RunMode.Iterations,
            NumberOfIterations = 5,
            TestMode = TestMode.Test,
            SkipWarmups = true)]
        [CounterThroughputAssertion(TransformCounterName, MustBe.GreaterThanOrEqualTo, 5200)]
        public void TransformTimedThroughput()
        {
            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.ElapsedMilliseconds < PerIterationRunTime.TotalMilliseconds)
            {
                //TransactionTransformer.Transform will be the only queued callback
                _compositeTestAgent.ExecuteThreadPoolQueuedCallbacks();
                _counter.Increment();
            }

            stopWatch.Stop();
        }

        private void CreateTransactionAndSegments()
        {
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");

            _agent.StartCustomSegmentOrThrow(Guid.NewGuid().ToString()).End();

            _agent.StartCustomSegmentOrThrow(Guid.NewGuid().ToString()).End();

            _agent.StartCustomSegmentOrThrow(Guid.NewGuid().ToString()).End();

            _agent.StartCustomSegmentOrThrow(Guid.NewGuid().ToString()).End();

            _agent.StartMethodSegmentOrThrow("MyType", "MyMethod").End();

            _agent.StartMethodSegmentOrThrow("MyType2", "MyMethod").End();

            _agent.StartMethodSegmentOrThrow("MyType3", "MyMethod").End();

            _agent.StartMethodSegmentOrThrow("MyType4", "MyMethod").End();

            _agent.StartMethodSegmentOrThrow("MyType5", "MyMethod").End();

            transaction.SetUri(Guid.NewGuid().ToString());
            transaction.SetHttpResponseStatusCode(200);
            transaction.SetOriginalUri(Guid.NewGuid().ToString());
            transaction.SetQueueTime(TimeSpan.FromMilliseconds(20));
            transaction.SetReferrerUri(Guid.NewGuid().ToString());

            segment.End();
            transaction.End();
        }

        [PerfCleanup]
        public void Cleanup()
        {
            _compositeTestAgent.Dispose();
        }
    }
}
