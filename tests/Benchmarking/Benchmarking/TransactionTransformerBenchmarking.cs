using System;
using System.Diagnostics;
using CompositeTests;
using NBench;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace Benchmarking
{
	public class TransactionTransformerBenchmarking
	{
		private const string TransformCounterName = "TransformCounter";
		private static readonly TimeSpan PerIterationRunTime = TimeSpan.FromMinutes(1);

		private Counter _counter;
		private CompositeTestAgent _compositeTestAgent;
		private IAgentWrapperApi _agentWrapperApi;
		
		[PerfSetup]
		public void Setup(BenchmarkContext context)
		{
			_counter = context.GetCounter(TransformCounterName);
			_compositeTestAgent = new CompositeTestAgent();
			_agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

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
			var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");

			var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");

			_agentWrapperApi.StartCustomSegmentOrThrow(Guid.NewGuid().ToString()).End();

			_agentWrapperApi.StartCustomSegmentOrThrow(Guid.NewGuid().ToString()).End();

			_agentWrapperApi.StartCustomSegmentOrThrow(Guid.NewGuid().ToString()).End();

			_agentWrapperApi.StartCustomSegmentOrThrow(Guid.NewGuid().ToString()).End();

			_agentWrapperApi.StartMethodSegmentOrThrow("MyType", "MyMethod").End();

			_agentWrapperApi.StartMethodSegmentOrThrow("MyType2", "MyMethod").End();

			_agentWrapperApi.StartMethodSegmentOrThrow("MyType3", "MyMethod").End();

			_agentWrapperApi.StartMethodSegmentOrThrow("MyType4", "MyMethod").End();

			_agentWrapperApi.StartMethodSegmentOrThrow("MyType5", "MyMethod").End();

			transaction.SetUri(Guid.NewGuid().ToString());
			transaction.SetHttpResponseStatusCode(200);
			transaction.SetOriginalUri(Guid.NewGuid().ToString());
			transaction.SetPath(Guid.NewGuid().ToString());
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
