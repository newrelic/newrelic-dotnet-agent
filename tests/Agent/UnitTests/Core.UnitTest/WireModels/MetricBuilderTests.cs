// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using realWireModels = NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture]
    public class MetricBuilderTests
    {
        private realWireModels.IMetricBuilder _metricBuilder;

        [SetUp]
        public void SetUp()
        {
            var metricNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => metricNameService.RenameMetric(Arg.IsAny<string>()))
                .Returns(metricName => metricName);
            _metricBuilder = new MetricWireModel.MetricBuilder(metricNameService);
        }

        [Test]
        public void BuildMemoryPhysicalMetric()
        {
            const int RawBytes = 1024;
            var actualMetric = _metricBuilder.TryBuildMemoryPhysicalMetric(RawBytes);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.MemoryPhysical, actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildByteData(RawBytes), actualMetric.Data)
            );
        }

        [Test]
        public void BuildMemoryWorkingSetMetric()
        {
            const int RawBytes = 1536;
            var actualMetric = _metricBuilder.TryBuildMemoryWorkingSetMetric(RawBytes);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.MemoryWorkingSet, actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildByteData(RawBytes), actualMetric.Data)
            );
        }

        [Test]
        public void BuildThreadpoolUsageStatsMetric()
        {
            const int RawValue = 3;
            var threadType = Samplers.ThreadType.Worker;
            var threadStatus = Samplers.ThreadStatus.Available;
            var actualMetric = _metricBuilder.TryBuildThreadpoolUsageStatsMetric(threadType, threadStatus, RawValue);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.GetThreadpoolUsageStatsName(threadType, threadStatus), actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildGaugeValue(RawValue), actualMetric.Data)
            );
        }

        [Test]
        public void BuildThreadpoolThroughputStatsMetric()
        {
            const int RawValue = 3;
            var throughputStatsType = Samplers.ThreadpoolThroughputStatsType.Started;
            var actualMetric = _metricBuilder.TryBuildThreadpoolThroughputStatsMetric(throughputStatsType, RawValue);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.GetThreadpoolThroughputStatsName(throughputStatsType), actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildGaugeValue(RawValue), actualMetric.Data)
            );
        }

        [Test]
        public void BuildGCBytesMetric()
        {
            const long RawByteValue = 123456;
            var gcSampleType = Samplers.GCSampleType.Gen0Size;
            var actualMetric = _metricBuilder.TryBuildGCBytesMetric(gcSampleType, RawByteValue);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.GetGCMetricName(gcSampleType), actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildByteData(RawByteValue), actualMetric.Data)
            );
        }

        [Test]
        public void BuildGCCountMetric()
        {
            const int RawCountValue = 3;
            var gcSampleType = Samplers.GCSampleType.Gen0CollectionCount;
            var actualMetric = _metricBuilder.TryBuildGCCountMetric(gcSampleType, RawCountValue);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.GetGCMetricName(gcSampleType), actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildCountData(RawCountValue), actualMetric.Data)
            );
        }

        [Test]
        public void BuildGCPercentMetric()
        {
            const float RawPercentageValue = 0.8f;
            var gcSampleType = Samplers.GCSampleType.PercentTimeInGc;
            var actualMetric = _metricBuilder.TryBuildGCPercentMetric(gcSampleType, RawPercentageValue);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.GetGCMetricName(gcSampleType), actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildPercentageData(RawPercentageValue), actualMetric.Data)
            );
        }

        [Test]
        public void BuildGCGaugeMetric()
        {
            const float RawValue = 3000f;
            var gcSampleType = Samplers.GCSampleType.HandlesCount;
            var actualMetric = _metricBuilder.TryBuildGCGaugeMetric(gcSampleType, RawValue);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.GetGCMetricName(gcSampleType), actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildGaugeValue(RawValue), actualMetric.Data)
            );
        }

        [Test]
        public void BuildSupportabilityCountMetric_DefuaultCount()
        {
            const string MetricName = "WCFClient/BindingType/BasicHttpBinding";
            var actualMetric = _metricBuilder.TryBuildSupportabilityCountMetric(MetricName);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.GetSupportabilityName(MetricName), actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildCountData(1), actualMetric.Data)
            );
        }

        [Test]
        public void BuildSupportabilityCountMetric_SuppliedCount()
        {
            const string MetricName = "WCFClient/BindingType/BasicHttpBinding";
            var actualMetric = _metricBuilder.TryBuildSupportabilityCountMetric(MetricName, 2);
            NrAssert.Multiple(
                () => Assert.AreEqual(MetricNames.GetSupportabilityName(MetricName), actualMetric.MetricName.Name),
                () => Assert.AreEqual(MetricDataWireModel.BuildCountData(2), actualMetric.Data)
            );
        }
    }
}
