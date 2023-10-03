// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class MemorySampleTransformerTests
    {
        private IMetricBuilder _metricBuilder;

        private IMetricAggregator _metricAggregator;

        private const float BytesPerMb = 1048576f;
        private const long PrivateBytesTestValue = 2348987234L;
        private const long WorkingSetTestValue = 42445745745L;

        [SetUp]
        public void SetUp()
        {
            _metricBuilder = new MetricWireModel.MetricBuilder(new MetricNameService());
            _metricAggregator = Mock.Create<IMetricAggregator>();
        }

        [Test]
        public void TransformSample_CallExpectedMethods()
        {
            var expectedMemoryPhysicalMetric = _metricBuilder.TryBuildMemoryPhysicalMetric(2);
            var expectedMemoryWorkingSetMetric = _metricBuilder.TryBuildMemoryWorkingSetMetric(4);

            var sample = new ImmutableMemorySample(2, 4);
            Transform(sample);

            Mock.Assert(() => _metricAggregator.Collect(expectedMemoryPhysicalMetric));
            Mock.Assert(() => _metricAggregator.Collect(expectedMemoryWorkingSetMetric));
        }

        [Test]
        public void TransformSample_CreatesUnscopedMetrics()
        {
            var generatedMetrics = new Dictionary<string, MetricDataWireModel>();

            long expectedMemoryPhysicalValue = 2348987234L;
            float expectedMemoryPhysicalValueAsFloat = expectedMemoryPhysicalValue / BytesPerMb;
            long expectedMemoryWorkingSetValue = 42445745745L;
            float expectedMemoryWorkingSetValueAsFloat = expectedMemoryWorkingSetValue / BytesPerMb;

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>())).DoInstead<MetricWireModel>(m => generatedMetrics.Add(m.MetricName.Name, m.Data));

            var sample = new ImmutableMemorySample(expectedMemoryPhysicalValue, expectedMemoryWorkingSetValue);
            Transform(sample);

            NrAssert.Multiple(
                () => Assert.AreEqual(2, generatedMetrics.Count),
                () => MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.MemoryPhysical, expectedMemoryPhysicalValueAsFloat),
                () => MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.MemoryWorkingSet, expectedMemoryWorkingSetValueAsFloat)
            );
        }

        [Test]
        public void TransformSample_CreatesNoMemoryMetricsValueZero()
        {
            var generatedMetrics = new Dictionary<string, MetricDataWireModel>();

            long expectedMemoryPhysicalValue = 0L;
            long expectedMemoryWorkingSetValue = 0L;

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>())).DoInstead<MetricWireModel>(m => generatedMetrics.Add(m.MetricName.Name, m.Data));

            var sample = new ImmutableMemorySample(expectedMemoryPhysicalValue, expectedMemoryWorkingSetValue);
            Transform(sample);

            Assert.IsEmpty(generatedMetrics);
        }

        [TestCase(true, PrivateBytesTestValue / BytesPerMb, WorkingSetTestValue / BytesPerMb)]
        [TestCase(false, WorkingSetTestValue / BytesPerMb, WorkingSetTestValue / BytesPerMb)]
        public void TransformSample_UsesAppropriateMemorySampleForPlatform(bool isWindows, float expectedMemoryPhysicalValue, float expectedWorkingSetValue)
        {
            var generatedMetrics = new Dictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>())).DoInstead<MetricWireModel>(m => generatedMetrics.Add(m.MetricName.Name, m.Data));

            var sample = new ImmutableMemorySample(PrivateBytesTestValue, WorkingSetTestValue);
            Transform(sample, isWindows);

            NrAssert.Multiple(
                () => Assert.AreEqual(2, generatedMetrics.Count),
                () => MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.MemoryPhysical, expectedMemoryPhysicalValue),
                () => MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.MemoryWorkingSet, expectedWorkingSetValue)
            );
        }

        private void Transform(ImmutableMemorySample sample)
        {
            Transform(sample, true);
        }

        private void Transform(ImmutableMemorySample sample, bool isWindows)
        {
            var transformer = new MemorySampleTransformer(_metricBuilder, _metricAggregator, () => isWindows);
            transformer.Transform(sample);
        }
    }
}
