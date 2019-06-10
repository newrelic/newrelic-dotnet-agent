using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers
{
	public interface IThreadStatsSampleTransformer
	{
		void Transform(ThreadStatsSample threadpoolStats);
	}


	public class ThreadStatsSampleTransformer : IThreadStatsSampleTransformer
	{
		private readonly IMetricBuilder _metricBuilder;

		private readonly IMetricAggregator _metricAggregator;

		public ThreadStatsSampleTransformer(IMetricBuilder metricBuilder, IMetricAggregator metricAggregator) 
		{
			_metricBuilder = metricBuilder;
			_metricAggregator = metricAggregator;
		}

		public void Transform(ThreadStatsSample threadpoolStats)
		{
			var workerThreadsAvail = _metricBuilder.TryBuildThreadpoolUsageStatsMetric(ThreadType.Worker, ThreadStatus.Available, threadpoolStats.WorkerCountThreadsAvail);
			var workerThreadsUsed = _metricBuilder.TryBuildThreadpoolUsageStatsMetric(ThreadType.Worker, ThreadStatus.InUse, threadpoolStats.WorkerCountThreadsUsed);
			var completionThreadsAvail = _metricBuilder.TryBuildThreadpoolUsageStatsMetric(ThreadType.Completion, ThreadStatus.Available, threadpoolStats.CompletionCountThreadsAvail);
			var completionThreadsUsed =_metricBuilder.TryBuildThreadpoolUsageStatsMetric(ThreadType.Completion, ThreadStatus.InUse, threadpoolStats.CompletionCountThreadsUsed);

			RecordMetrics(workerThreadsAvail, workerThreadsUsed, completionThreadsAvail, completionThreadsUsed);
		}

		private void RecordMetrics(params MetricWireModel[] metrics)
		{
			foreach(var metric in metrics)
			{
				_metricAggregator.Collect(metric);
			}
		}
	}
}