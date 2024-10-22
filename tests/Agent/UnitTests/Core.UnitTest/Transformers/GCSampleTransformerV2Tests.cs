// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class GCSampleTransformerV2Tests
    {
        private IMetricBuilder _metricBuilder;
        private IMetricAggregator _metricAggregator;
        private GCSampleTransformerV2 _transformer;

        [SetUp]
        public void SetUp()
        {
            _metricBuilder = new MetricWireModel.MetricBuilder(new MetricNameService());

            _metricAggregator = Mock.Create<IMetricAggregator>();

            _transformer = new GCSampleTransformerV2(_metricBuilder, _metricAggregator);
        }

        [Test]
        public void Transform_ShouldUpdateCurrentAndPreviousSamples()
        {
            // Arrange
            var sample = CreateSample();
            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>()));

            // Act
            _transformer.Transform(sample);

            // Assert

            Assert.Multiple(() =>
            {
                Assert.That(_transformer.PreviousSample, Is.Not.Null);
                Assert.That(_transformer.CurrentSample, Is.EqualTo(sample));
            });
        }

        [Test]
        public void Transform_ShouldBuildAndRecordMetrics()
        {
            // Arrange
            var sample = CreateSample();

            var generatedMetrics = new Dictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>())).DoInstead<MetricWireModel>(m => generatedMetrics.Add(m.MetricNameModel.Name, m.DataModel));

            // Act
            _transformer.Transform(sample);

            // Assert
            const float bytesPerMb = 1048576f;
            Assert.Multiple(() =>
            {
                Assert.That(generatedMetrics, Has.Count.EqualTo(18));
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.TotalAllocatedMemory), sample.TotalAllocatedBytes / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.TotalCommittedMemory), sample.TotalCommittedBytes / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.TotalHeapMemory), sample.TotalMemoryBytes / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen0Size), sample.GCHeapSizesBytes[0] / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen1Size), sample.GCHeapSizesBytes[1] / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen2Size), sample.GCHeapSizesBytes[2] / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.LOHSize), sample.GCHeapSizesBytes[3] / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.POHSize), sample.GCHeapSizesBytes[4] / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen0FragmentationSize), sample.GCFragmentationSizesBytes[0] / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen1FragmentationSize), sample.GCFragmentationSizesBytes[1] / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen2FragmentationSize), sample.GCFragmentationSizesBytes[2] / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.LOHFragmentationSize), sample.GCFragmentationSizesBytes[3] / bytesPerMb);
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.POHFragmentationSize), sample.GCFragmentationSizesBytes[4] / bytesPerMb);
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen0CollectionCount), sample.GCCollectionCounts[0]);
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen1CollectionCount), sample.GCCollectionCounts[1]);
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen2CollectionCount), sample.GCCollectionCounts[2]);
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.LOHCollectionCount), sample.GCCollectionCounts[3]);
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.POHCollectionCount), sample.GCCollectionCounts[4]);
            });
        }

        [Test]
        public void Transform_ShouldRecordZeroMetric_WhenCurrentValueIsLessThanPreviousValue()
        {
            // Arrange
            var previousSample = CreateSampleWithCollectionCounts([3, 3, 3, 3, 3]);
            var currentSample = CreateSampleWithCollectionCounts([1, 1, 1, 1, 1]);

            var generatedMetrics = new Dictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>())).DoInstead<MetricWireModel>(m => generatedMetrics.Add(m.MetricNameModel.Name, m.DataModel));

            // Act
            _transformer.Transform(previousSample);

            generatedMetrics.Clear();

            _transformer.Transform(currentSample);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(generatedMetrics, Has.Count.EqualTo(18));
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen0CollectionCount), 0);
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen1CollectionCount), 0);
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.Gen2CollectionCount), 0);
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.LOHCollectionCount), 0);
                MetricTestHelpers.CompareCountMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.POHCollectionCount), 0);
            });
        }


        private ImmutableGCSample CreateSample()
        {
            return new ImmutableGCSample(
                lastSampleTime: System.DateTime.UtcNow.AddMinutes(-1),
                currentSampleTime: System.DateTime.UtcNow,
                totalMemoryBytes: 1024L,
                totalAllocatedBytes: 2048L,
                totalCommittedBytes: 4096L,
                heapSizesBytes: [100, 200, 300, 400, 500],
                rawCollectionCounts: [5, 4, 3, 2, 1],
                fragmentationSizesBytes: [10, 20, 30, 40, 50]
            );
        }

        private ImmutableGCSample CreateSampleWithCollectionCounts(int[] collectionCounts)
        {
            return new ImmutableGCSample(
                lastSampleTime: System.DateTime.UtcNow.AddMinutes(-1),
                currentSampleTime: System.DateTime.UtcNow,
                totalMemoryBytes: 1024L,
                totalAllocatedBytes: 2048L,
                totalCommittedBytes: 4096L,
                heapSizesBytes: [100, 200, 300, 400, 500],
                rawCollectionCounts: collectionCounts,
                fragmentationSizesBytes: [10, 20, 30, 40, 50]
            );
        }

        [Test]
        public void Transform_ShouldRecordZeroMetric_WhenCurrentAllocatedMemoryIsLessThanPreviousAllocatedMemory()
        {
            // Arrange
            var previousSample = CreateSampleWithAllocatedBytes(2048L);
            var currentSample = CreateSampleWithAllocatedBytes(1024L);

            var generatedMetrics = new Dictionary<string, MetricDataWireModel>();

            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>())).DoInstead<MetricWireModel>(m => generatedMetrics.Add(m.MetricNameModel.Name, m.DataModel));

            // Act
            _transformer.Transform(previousSample);

            generatedMetrics.Clear();

            _transformer.Transform(currentSample);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(generatedMetrics, Has.Count.EqualTo(18));
                MetricTestHelpers.CompareMetric(generatedMetrics, MetricNames.GetGCMetricName(GCSampleType.TotalAllocatedMemory), 0);
            });
        }

        private ImmutableGCSample CreateSampleWithAllocatedBytes(long allocatedBytes)
        {
            return new ImmutableGCSample(
                lastSampleTime: System.DateTime.UtcNow.AddMinutes(-1),
                currentSampleTime: System.DateTime.UtcNow,
                totalMemoryBytes: 1024L,
                totalAllocatedBytes: allocatedBytes,
                totalCommittedBytes: 4096L,
                heapSizesBytes: [100, 200, 300, 400, 500],
                rawCollectionCounts: [5, 4, 3, 2, 1],
                fragmentationSizesBytes: [10, 20, 30, 40, 50]
            );
        }
    }
}
