// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class CpuSampleTransformerTests
    {
        private CpuSampleTransformer _cpuSampleTransformer;

        private IMetricBuilder _metricBuilder;

        private IMetricAggregator _metricAggregator;

        [SetUp]
        public void SetUp()
        {
            _metricBuilder = Mock.Create<IMetricBuilder>();
            _metricAggregator = Mock.Create<IMetricAggregator>();

            _cpuSampleTransformer = new CpuSampleTransformer(_metricBuilder, _metricAggregator);
        }

        [Test]
        public void TransformSample_CreatesUnscopedMetrics()
        {
            var expectedCpuTimeMetric = _metricBuilder.TryBuildCpuUserTimeMetric(TimeSpan.FromSeconds(1));
            var expectedCpuUtilizationMetric = _metricBuilder.TryBuildCpuUserUtilizationMetric(0.5f);
            //var generatedMetrics = new List<MetricWireModel>();
            Mock.Arrange(() => _metricBuilder.TryBuildCpuUserTimeMetric(Arg.IsAny<TimeSpan>())).Returns(expectedCpuTimeMetric);
            Mock.Arrange(() => _metricBuilder.TryBuildCpuUserUtilizationMetric(Arg.IsAny<float>())).Returns(expectedCpuUtilizationMetric);
            //Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>())).DoInstead<MetricWireModel>(metric => generatedMetrics.Add(metric));

            var sample = new ImmutableCpuSample(1, DateTime.UtcNow, TimeSpan.FromSeconds(1), DateTime.UtcNow, TimeSpan.FromSeconds(1));
            _cpuSampleTransformer.Transform(sample);

            Mock.Assert(() => _metricBuilder.TryBuildCpuUserTimeMetric(TimeSpan.FromSeconds(1)));
            Mock.Assert(() => _metricBuilder.TryBuildCpuUserUtilizationMetric(0.5f));
            //ClassicAssert.IsTrue(generatedMetrics.Contains(expectedCpuTimeMetric));
            //ClassicAssert.IsTrue(generatedMetrics.Contains(expectedCpuUtilizationMetric));
        }
    }
}
