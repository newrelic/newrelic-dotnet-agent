using JetBrains.Annotations;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers
{
	public interface IMemorySampleTransformer
	{
		void Transform([NotNull] ImmutableMemorySample sample);
	}

	public class MemorySampleTransformer : IMemorySampleTransformer
	{
		[NotNull]
		protected readonly IMetricBuilder MetricBuilder;

		[NotNull]
		private readonly IMetricAggregator _metricAggregator;

		public MemorySampleTransformer([NotNull] IMetricBuilder metricBuilder, [NotNull] IMetricAggregator metricAggregator)
		{
			MetricBuilder = metricBuilder;
			_metricAggregator = metricAggregator;
		}

		public void Transform(ImmutableMemorySample sample)
		{
			var unscopedCpuUserTimeMetric = MetricBuilder.TryBuildMemoryPhysicalMetric(sample.MemoryPhysical);
			RecordMetric(unscopedCpuUserTimeMetric);
		}

		private void RecordMetric([CanBeNull] MetricWireModel metric)
		{
			if (metric == null)
				return;

			_metricAggregator.Collect(metric);
		}
	}
}