using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace CompositeTests
{
	[TestFixture]
	public class ApiSupportabilityMetricTests
	{
		private const string SupportabilityMetricPrefix = "Supportability/ApiInvocation/";

		private static CompositeTestAgent _compositeTestAgent;
		private IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;
		private IAgentApi _agentApi;
		private IAgentWrapperApi _agentWrapperApi;

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();
			_agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();
			_apiSupportabilityMetricCounters = _compositeTestAgent.Container.Resolve<IApiSupportabilityMetricCounters>();
			_agentApi = _compositeTestAgent.GetAgentApiImplementation();
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[Test]
		public void CreateDistributedTracePayloadTest()
		{
			CallTransactionApiBridgeMethod(transactionBridgeApi => transactionBridgeApi.CreateDistributedTracePayload(), "CreateDistributedTracePayload");
		}

		[Test]
		public void AcceptDistributedTracePayloadTest()
		{
			CallTransactionApiBridgeMethod(transactionBridgeApi => transactionBridgeApi.AcceptDistributedTracePayload("testString", 0), "AcceptDistributedTracePayload");
		}

		[Test]
		public void CurrentTransactionTest()
		{
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();
			var agentBridgeApi = new AgentBridgeApi(agentWrapperApi, _apiSupportabilityMetricCounters);

			var currentTransaction = agentBridgeApi.CurrentTransaction;

			HarvestAndValidateMetric("CurrentTransaction");
		}

		[Test]
		public void DisableBrowserMonitoring()
		{
			_agentApi.DisableBrowserMonitoring();
			HarvestAndValidateMetric("DisableBrowserMonitoring");
		}

		[Test]
		public void AddCustomParameter()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.AddCustomParameter("customParameter", "1234"), "AddCustomParameter");
		}

		[Test]
		public void GetBrowserTimingHeader()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.GetBrowserTimingHeader(), "GetBrowserTimingHeader");
		}

		[Test]
		public void GetBrowserTimingFooter()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.GetBrowserTimingFooter(), "GetBrowserTimingFooter");
		}

		[Test]
		public void IgnoreApdex()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.IgnoreApdex(), "IgnoreApdex");
		}

		[Test]
		public void IgnoreTransaction()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.IgnoreTransaction(), "IgnoreTransaction");
		}

		[Test]
		public void IncrementCounter()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.IncrementCounter("fred"), "IncrementCounter");
		}

		[Test]
		public void NoticeError()
		{
			var exception = new IndexOutOfRangeException();
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.NoticeError(exception), "NoticeError");
		}

		[Test]
		public void RecordCustomEvent()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.RecordCustomEvent("customEvent", null), "RecordCustomEvent");
		}

		[Test]
		public void RecordMetric()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.RecordMetric("metricName", 1f), "RecordMetric");
		}

		[Test]
		public void RecordResponseTimeMetric()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.RecordResponseTimeMetric("responseTimeMetric", 1234L), "RecordResponseTimeMetric");
		}

		[Test]
		public void SetApplicationName()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.SetApplicationName("applicationName"), "SetApplicationName");
		}

		[Test]
		public void SetTransactionName()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.SetTransactionName("custom", "transactionName"), "SetTransactionName");
		}

		[Test]
		public void SetUserParameters()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.SetUserParameters("userName", "accountName", "productName"), "SetUserParameters");
		}

		[Test]
		public void StartAgent()
		{
			CallAgentApiMethodRequiringTransaction(_agentApi => _agentApi.StartAgent(), "StartAgent");
		}

		private void CallAgentApiMethodRequiringTransaction(Action<IAgentApi> apiMethod, string expectedMetricName)
		{
			using(var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "TransactionName"))
			{
				apiMethod(_agentApi);
			}

			HarvestAndValidateMetric(expectedMetricName);
		}

		private void HarvestAndValidateMetric(string expectedMetricName)
		{
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + expectedMetricName, CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
		}

		private void CallTransactionApiBridgeMethod(Action<TransactionBridgeApi> apiMethod, string expectedMetricName)
		{
			using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "TransactionName"))
			{
				var transactionBridgeApi = new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters);
				var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");

				apiMethod(transactionBridgeApi);

				segment.End();
			}

			HarvestAndValidateMetric(expectedMetricName);
		}
	}
}
