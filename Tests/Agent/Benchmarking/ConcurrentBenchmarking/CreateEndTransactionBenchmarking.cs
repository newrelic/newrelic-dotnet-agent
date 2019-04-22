using System;
using System.Diagnostics;
using CompositeTests;
using NBench;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace ConcurrentBenchmarking
{
	public class CreateEndTransactionBenchmarking
	{
		private const string TransactionCounterName = "TransactionCounter";
		private static readonly TimeSpan PerIterationRunTime = TimeSpan.FromMinutes(3);

		private Counter _counter;
		private CompositeTestAgent _compositeTestAgent;
		private IAgent _agent;

		[PerfSetup]
		public void Setup(BenchmarkContext context)
		{
			_counter = context.GetCounter(TransactionCounterName);
			_compositeTestAgent = new CompositeTestAgent(shouldAllowThreads: true, includeAsyncLocalStorage: false);
			_agent = _compositeTestAgent.GetAgent();
		}

		[PerfBenchmark(
			Description = "Throughput test for Creating and Ending transactions.",
			RunMode = RunMode.Iterations,
			NumberOfIterations = 2,
			TestMode = TestMode.Test,
			SkipWarmups = true)]
		[CounterThroughputAssertion(TransactionCounterName, MustBe.GreaterThanOrEqualTo, 24300)]
		public void TransactionTimedThroughput()
		{
			var stopWatch = Stopwatch.StartNew();

			while (stopWatch.ElapsedMilliseconds < PerIterationRunTime.TotalMilliseconds)
			{
				var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
				_agent.StartTransactionSegmentOrThrow("segmentName").End();
				tx.End();

				_counter.Increment();
			}

			stopWatch.Stop();
		}


		[PerfBenchmark(
			Description = "Throughput test for Creating/Ending transactions with lots of similar segments.",
			RunMode = RunMode.Iterations,
			NumberOfIterations = 3,
			TestMode = TestMode.Test,
			SkipWarmups = true)]
		[CounterThroughputAssertion(TransactionCounterName, MustBe.GreaterThanOrEqualTo, 24300)]
		public void TransactionTimedLotsOfSimilarSegments()
		{
			var stopWatch = Stopwatch.StartNew();

			while (stopWatch.ElapsedMilliseconds < PerIterationRunTime.TotalMilliseconds)
			{
				var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
				for (var x = 0; x < 1000; x++)
				{
					var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
					segment.End();
				}
				tx.End();

				_counter.Increment();
			}

			stopWatch.Stop();
		}

		[PerfBenchmark(
		Description = "Throughput test for Creating/Ending transactions with multiple kinds of segments.",
		RunMode = RunMode.Iterations,
		NumberOfIterations = 3,
		TestMode = TestMode.Test,
		SkipWarmups = true)]
		[CounterThroughputAssertion(TransactionCounterName, MustBe.GreaterThanOrEqualTo, 24300)]
		public void TransactionSqlSegments()
		{
			var stopWatch = Stopwatch.StartNew();

			while (stopWatch.ElapsedMilliseconds < PerIterationRunTime.TotalMilliseconds)
			{
				var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
				var segment = _agent.StartTransactionSegmentOrThrow("segmentName");

				for (var x = 0; x < 500; x++)
				{
					_agent.StartDatastoreRequestSegmentOrThrow("operation", DatastoreVendor.MongoDB, "model").End();
					_agent.StartDatastoreRequestSegmentOrThrow("oper", DatastoreVendor.MySQL, "mod", host: "localhost", portPathOrId: "8080", databaseName: "myDB").End();
				}

				segment.End();
				tx.End();

				_counter.Increment();
			}

			stopWatch.Stop();
		}

		[PerfBenchmark(
		Description = "Throughput test for Creating/Ending transactions with multiple SQL traces.",
		RunMode = RunMode.Iterations,
		NumberOfIterations = 3,
		TestMode = TestMode.Test,
		SkipWarmups = true)]
		[CounterThroughputAssertion(TransactionCounterName, MustBe.GreaterThanOrEqualTo, 10)]
		public void TransactionMultipleSqlTraces()
		{
			var stopWatch = Stopwatch.StartNew();

			_compositeTestAgent.LocalConfiguration.slowSql.enabled = true;
			_compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
			_compositeTestAgent.PushConfiguration();

			while (stopWatch.ElapsedMilliseconds < PerIterationRunTime.TotalMilliseconds)
			{
				var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
				var segment = _agent.StartTransactionSegmentOrThrow("segmentName");

				for (var x = 0; x < 1000; x++)
				{
					var commandText = "SELECT * FROM Table" + x.ToString(); // sqlId derived from commandText must be unique to generate multiple non-aggregated sqltraces
					var seg1 = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Model", commandText);
					seg1.End();
				}

				segment.End();
				tx.End();

				_counter.Increment();
			}

			stopWatch.Stop();
		}

		[PerfCleanup]
		public void Cleanup()
		{
			_compositeTestAgent.Dispose();
		}
	}
}
