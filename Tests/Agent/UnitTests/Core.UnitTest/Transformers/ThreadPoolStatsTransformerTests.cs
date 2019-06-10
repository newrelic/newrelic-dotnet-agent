using System.Collections.Generic;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;

namespace NewRelic.Agent.Core.Transformers
{
	[TestFixture]
	public class ThreadPoolStatsTransformerTests
	{
		private IThreadStatsSampleTransformer _threadStatsTransformer;

		private IMetricBuilder _metricBuilder;

		private IMetricAggregator _metricAggregator;

		[SetUp]
		public void SetUp()
		{
			var metricNameService = new MetricNameService();
			_metricBuilder = new MetricBuilder(metricNameService);
			_metricAggregator = Mock.Create<IMetricAggregator>();

			_threadStatsTransformer = new ThreadStatsSampleTransformer(_metricBuilder, _metricAggregator);
		}

		[Test]
		public void TransformSample_CreatesCorrectMetricValues()
		{
			const int countWorkerThreadsRemaining = 83;
			const int countWorkerThreadsInUse = 17;
			const int countCompletionThreadsRemaining = 180;
			const int countCompletionThreadsInUse = 20;

			var generatedMetrics = new Dictionary<string,MetricDataWireModel>();

			Mock.Arrange(() => _metricAggregator
				.Collect(Arg.IsAny<MetricWireModel>()))
				.DoInstead<MetricWireModel>(m => generatedMetrics.Add(m.MetricName.Name,m.Data));

			var sample = new ThreadStatsSample(countWorkerThreadsRemaining + countWorkerThreadsInUse, countWorkerThreadsRemaining, countCompletionThreadsRemaining + countCompletionThreadsInUse, countCompletionThreadsRemaining);

			_threadStatsTransformer.Transform(sample);

			NrAssert.Multiple(
				() => Assert.AreEqual(4, generatedMetrics.Count),
				() => MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetThreadpoolUsageStatsName(ThreadType.Worker, ThreadStatus.InUse), countWorkerThreadsInUse),
				() => MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetThreadpoolUsageStatsName(ThreadType.Worker, ThreadStatus.Available), countWorkerThreadsRemaining),
				() => MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetThreadpoolUsageStatsName(ThreadType.Completion, ThreadStatus.InUse), countCompletionThreadsInUse),
				() => MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetThreadpoolUsageStatsName(ThreadType.Completion, ThreadStatus.Available), countCompletionThreadsRemaining)
			);
		}
	}
}
