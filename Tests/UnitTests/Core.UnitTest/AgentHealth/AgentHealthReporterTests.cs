using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.AgentHealth
{
	[TestFixture]
	public class AgentHealthReporterTests
	{
		[NotNull]
		private AgentHealthReporter _agentHealthReporter;

		[NotNull]
		private List<MetricWireModel> _publishedMetrics;

		[SetUp]
		public void SetUp()
		{
			var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
			_agentHealthReporter = new AgentHealthReporter(metricBuilder, Mock.Create<IScheduler>());

			_publishedMetrics = new List<MetricWireModel>();
			_agentHealthReporter.RegisterPublishMetricHandler(metric => _publishedMetrics.Add(metric));
		}

		[Test]
		public void ReportPreHarvest_SendsExpectedMetrics()
		{
			_agentHealthReporter.ReportAgentVersion("1.0", "foo");
			Assert.AreEqual(2, _publishedMetrics.Count);
			var metric1 = _publishedMetrics.ElementAt(0);
			var metric2 = _publishedMetrics.ElementAt(1);
			NrAssert.Multiple(
				() => Assert.AreEqual("Supportability/AgentVersion/1.0", metric1.MetricName.Name),
				() => Assert.AreEqual(null, metric1.MetricName.Scope),
				() => Assert.AreEqual(1, metric1.Data.Value0),
				() => Assert.AreEqual(0, metric1.Data.Value1),
				() => Assert.AreEqual(0, metric1.Data.Value2),
				() => Assert.AreEqual(0, metric1.Data.Value3),
				() => Assert.AreEqual(0, metric1.Data.Value4),
				() => Assert.AreEqual(0, metric1.Data.Value5),

				() => Assert.AreEqual("Supportability/AgentVersion/foo/1.0", metric2.MetricName.Name),
				() => Assert.AreEqual(null, metric2.MetricName.Scope),
				() => Assert.AreEqual(1, metric2.Data.Value0),
				() => Assert.AreEqual(0, metric2.Data.Value1),
				() => Assert.AreEqual(0, metric2.Data.Value2),
				() => Assert.AreEqual(0, metric2.Data.Value3),
				() => Assert.AreEqual(0, metric2.Data.Value4),
				() => Assert.AreEqual(0, metric2.Data.Value5)
				);
		}
		
		[Test]
		public void ReportWrapperShutdown_SendsExpectedMetrics()
		{
			_agentHealthReporter.ReportWrapperShutdown(Mock.Create<IWrapper>(), new Method(typeof(String), "FooMethod", "FooParam"));
			Assert.AreEqual(3, _publishedMetrics.Count);
			var metric0 = _publishedMetrics.ElementAt(0);
			var metric1 = _publishedMetrics.ElementAt(1);
			var metric2 = _publishedMetrics.ElementAt(2);
			Assert.AreEqual("Supportability/WrapperShutdown/all", metric0.MetricName.Name);
			Assert.AreEqual("Supportability/WrapperShutdown/Castle.Proxies.IWrapperProxy/all", metric1.MetricName.Name);
			Assert.AreEqual("Supportability/WrapperShutdown/Castle.Proxies.IWrapperProxy/String.FooMethod", metric2.MetricName.Name);
		}

		[Test]
		public void GenerateExpectedCollectorErrorSupportabilityMetrics()
		{
			_agentHealthReporter.ReportSupportabilityCollectorErrorException("test_method_endpoint", TimeSpan.FromMilliseconds(1500), HttpStatusCode.InternalServerError);
			Assert.AreEqual(2, _publishedMetrics.Count);
			NrAssert.Multiple(
				() => Assert.AreEqual("Supportability/Agent/Collector/HTTPError/500", _publishedMetrics[0].MetricName.Name),
				() => Assert.AreEqual(1, _publishedMetrics[0].Data.Value0),
				() => Assert.AreEqual("Supportability/Agent/Collector/test_method_endpoint/Duration", _publishedMetrics[1].MetricName.Name),
				() => Assert.AreEqual(1, _publishedMetrics[1].Data.Value0),
				() => Assert.AreEqual(1.5, _publishedMetrics[1].Data.Value1)
			);
		}

		[Test]
		public void ShouldNotGenerateHttpErrorCollectorErrorSupportabilityMetric()
		{
			_agentHealthReporter.ReportSupportabilityCollectorErrorException("test_method_endpoint", TimeSpan.FromMilliseconds(1500), statusCode: null);
			Assert.AreEqual(1, _publishedMetrics.Count);
			NrAssert.Multiple(
				() => Assert.AreEqual("Supportability/Agent/Collector/test_method_endpoint/Duration", _publishedMetrics[0].MetricName.Name),
				() => Assert.AreEqual(1, _publishedMetrics[0].Data.Value0),
				() => Assert.AreEqual(1.5, _publishedMetrics[0].Data.Value1)
			);
		}
	}
}
