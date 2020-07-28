using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class MemorySampleTransformerTests
    {
        private MemorySampleTransformer _memorySampleTransformer;

        private IMetricBuilder _metricBuilder;

        private IMetricAggregator _metricAggregator;

        [SetUp]
        public void SetUp()
        {
            _metricBuilder = Mock.Create<IMetricBuilder>();
            _metricAggregator = Mock.Create<IMetricAggregator>();

            _memorySampleTransformer = new MemorySampleTransformer(_metricBuilder, _metricAggregator);
        }

        [Test]
        public void TransformSample_CreatesUnscopedMetric()
        {
            var expectedMetric = _metricBuilder.TryBuildMemoryPhysicalMetric(2f);
            Mock.Arrange(() => _metricBuilder.TryBuildMemoryPhysicalMetric(Arg.IsAny<float>())).Returns(expectedMetric);

            var sample = new ImmutableMemorySample(2f);
            _memorySampleTransformer.Transform(sample);

            Mock.Assert(() => _metricBuilder.TryBuildMemoryPhysicalMetric(2f));
            Mock.Assert(() => _metricAggregator.Collect(expectedMetric));
        }
    }
}
