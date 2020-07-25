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
        protected readonly IMetricBuilder MetricBuilder;
        private readonly IMetricAggregator _metricAggregator;

        public MemorySampleTransformer(IMetricBuilder metricBuilder, IMetricAggregator metricAggregator)
        {
            MetricBuilder = metricBuilder;
            _metricAggregator = metricAggregator;
        }

        public void Transform(ImmutableMemorySample sample)
        {
            var unscopedCpuUserTimeMetric = MetricBuilder.TryBuildMemoryPhysicalMetric(sample.MemoryPhysical);
            RecordMetric(unscopedCpuUserTimeMetric);
        }

        private void RecordMetric(MetricWireModel metric)
        {
            if (metric == null)
                return;

            _metricAggregator.Collect(metric);
        }
    }
}
