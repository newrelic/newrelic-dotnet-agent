using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.WireModels
{
	[TestFixture]
	public class MetricBuilderTests
	{
		private MetricWireModel.MetricBuilder _metricBuilder;

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
			const int rawBytes = 1024;

			var actualMetric = _metricBuilder.TryBuildMemoryPhysicalMetric(rawBytes);

			NrAssert.Multiple(
				() => Assert.AreEqual(MetricNames.MemoryPhysical, actualMetric.MetricName.Name),
				() => Assert.AreEqual(MetricDataWireModel.BuildByteData(rawBytes), actualMetric.Data)
			);
		}

		[Test]
		public void BuildMemoryWorkingSetMetric()
		{
			const int rawBytes = 1536;

			var actualMetric = _metricBuilder.TryBuildMemoryWorkingSetMetric(rawBytes);

			NrAssert.Multiple(
				() => Assert.AreEqual(MetricNames.MemoryWorkingSet, actualMetric.MetricName.Name),
				() => Assert.AreEqual(MetricDataWireModel.BuildByteData(rawBytes), actualMetric.Data)
			);
		}

		[Test]
		public void BuildThreadpoolUsageStatsMetric()
		{
			const int rawValue = 3;
			var threadType = Samplers.ThreadType.Worker;
			var threadStatus = Samplers.ThreadStatus.Available;

			var actualMetric = _metricBuilder.TryBuildThreadpoolUsageStatsMetric(threadType, threadStatus, rawValue);

			NrAssert.Multiple(
				() => Assert.AreEqual(MetricNames.GetThreadpoolUsageStatsName(threadType, threadStatus), actualMetric.MetricName.Name),
				() => Assert.AreEqual(MetricDataWireModel.BuildGaugeValue(rawValue), actualMetric.Data)
			);
		}

		[Test]
		public void BuildThreadpoolThroughputStatsMetric()
		{
			const int rawValue = 3;
			var throughputStatsType = Samplers.ThreadpoolThroughputStatsType.Started;

			var actualMetric = _metricBuilder.TryBuildThreadpoolThroughputStatsMetric(throughputStatsType, rawValue);

			NrAssert.Multiple(
				() => Assert.AreEqual(MetricNames.GetThreadpoolThroughputStatsName(throughputStatsType), actualMetric.MetricName.Name),
				() => Assert.AreEqual(MetricDataWireModel.BuildGaugeValue(rawValue), actualMetric.Data)
			);
		}

		[Test]
		public void BuildGCBytesMetric()
		{
			const long rawByteValue = 123456;
			var gcSampleType = Samplers.GCSampleType.Gen0Size;

			var actualMetric = _metricBuilder.TryBuildGCBytesMetric(gcSampleType, rawByteValue);

			NrAssert.Multiple(
				() => Assert.AreEqual(MetricNames.GetGCMetricName(gcSampleType), actualMetric.MetricName.Name),
				() => Assert.AreEqual(MetricDataWireModel.BuildByteData(rawByteValue), actualMetric.Data)
			);
		}

		[Test]
		public void BuildGCCountMetric()
		{
			const int rawCountValue = 3;
			var gcSampleType = Samplers.GCSampleType.Gen0CollectionCount;

			var actualMetric = _metricBuilder.TryBuildGCCountMetric(gcSampleType, rawCountValue);

			NrAssert.Multiple(
				() => Assert.AreEqual(MetricNames.GetGCMetricName(gcSampleType), actualMetric.MetricName.Name),
				() => Assert.AreEqual(MetricDataWireModel.BuildCountData(rawCountValue), actualMetric.Data)
			);
		}

		[Test]
		public void BuildGCPercentMetric()
		{
			const float rawPercentageValue = 0.8f;
			var gcSampleType = Samplers.GCSampleType.PercentTimeInGc;

			var actualMetric = _metricBuilder.TryBuildGCPercentMetric(gcSampleType, rawPercentageValue);

			NrAssert.Multiple(
				() => Assert.AreEqual(MetricNames.GetGCMetricName(gcSampleType), actualMetric.MetricName.Name),
				() => Assert.AreEqual(MetricDataWireModel.BuildPercentageData(rawPercentageValue), actualMetric.Data)
			);
		}

		[Test]
		public void BuildGCGaugeMetric()
		{
			const float rawValue = 3000f;
			var gcSampleType = Samplers.GCSampleType.HandlesCount;

			var actualMetric = _metricBuilder.TryBuildGCGaugeMetric(gcSampleType, rawValue);

			NrAssert.Multiple(
				() => Assert.AreEqual(MetricNames.GetGCMetricName(gcSampleType), actualMetric.MetricName.Name),
				() => Assert.AreEqual(MetricDataWireModel.BuildGaugeValue(rawValue), actualMetric.Data)
			);
		}
	}
}
