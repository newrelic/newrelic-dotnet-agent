using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers
{
	public interface IMemorySampleTransformer
	{
		void Transform(ImmutableMemorySample sample);
	}

	public class MemorySampleTransformer : IMemorySampleTransformer
	{
		private readonly IMetricBuilder _metricBuilder;

		private readonly IMetricAggregator _metricAggregator;

		private readonly IConfigurationService _configurationService;


		public MemorySampleTransformer(IMetricBuilder metricBuilder, IMetricAggregator metricAggregator, IConfigurationService configurationService)
		{
			_metricBuilder = metricBuilder;
			_metricAggregator = metricAggregator;
			_configurationService = configurationService;
		}

		public void Transform(ImmutableMemorySample sample)
		{
			// Do not create metrics if memory values are 0
			// Value may be 0 due to lack of support on called platform (i.e. Linux does not provide Process.PrivateMemorySize64)
			if (sample.MemoryPrivate > 0)
			{
				var unscopedMemoryPhysicalMetric = _metricBuilder.TryBuildMemoryPhysicalMetric(sample.MemoryPrivate);
				RecordMetric(unscopedMemoryPhysicalMetric);
			}

			if (_configurationService.Configuration.GenerateFullGcMemThreadMetricsEnabled)
			{
				if (sample.MemoryVirtual > 0)
				{
					var unscopedMemoryVirtualMetric = _metricBuilder.TryBuildMemoryVirtualMetric(sample.MemoryVirtual);
					RecordMetric(unscopedMemoryVirtualMetric);
				}

				if (sample.MemoryWorkingSet > 0)
				{
					var unscopedMemoryWorkingSetMetric = _metricBuilder.TryBuildMemoryWorkingSetMetric(sample.MemoryWorkingSet);
					RecordMetric(unscopedMemoryWorkingSetMetric);
				}
			}
		}

		private void RecordMetric(MetricWireModel metric)
		{
			if (metric == null)
				return;

			_metricAggregator.Collect(metric);
		}
	}
}