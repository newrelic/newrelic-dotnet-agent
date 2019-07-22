using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.DistributedTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompositeTests
{
	[TestFixture]
	public class DistributedTracingApiTests
	{
		private static readonly string _accountId = "acctid";
		private static readonly string _appId = "appid";
		private static readonly string _guid = "guid";
		private static readonly float _priority = .3f;
		private static readonly bool _sampled = true;
		private static readonly string _traceId = "traceid";
		private static readonly string _trustKey = "trustedkey";
		private static readonly string _type = "typeapp";
		private static readonly string _transactionId = "transactionId";

		private readonly DistributedTracePayload _distributedTracePayload = DistributedTracePayload.TryBuildOutgoingPayload(_type, _accountId, _appId, _guid, _traceId, _trustKey, _priority, _sampled, DateTime.UtcNow, _transactionId);
		private readonly string _emptyDistributedTracePayloadString = "";
		// "bad" JSON string created by adding an extra single quote to the beginning of the string
		private readonly string _badDistributedTracePayloadString = "{{\"v\":[0,1],\"d\":{\"ty\":\"App\",\"ac\":\"acctid\",\"ap\":\"appid\",\"tr\":\"EA7E29EBB63A42AB\",\"pr\":1.120429,\"sa\":true,\"ti\":1540398878608,\"tk\":\"trustedkey\",\"tx\":\"EA7E29EBB63A42AB\"}}";

		private static CompositeTestAgent _compositeTestAgent;
		private IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();
			_compositeTestAgent.ServerConfiguration.AccountId = _accountId;
			_compositeTestAgent.ServerConfiguration.TrustedAccountKey = _trustKey;
			_compositeTestAgent.ServerConfiguration.PrimaryApplicationId = _appId;
			_apiSupportabilityMetricCounters = _compositeTestAgent.Container.Resolve<IApiSupportabilityMetricCounters>();

		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[Test]
		public void ShouldNotCreateDistributedTracePayload()
		{
			var agentWrapperApi = _compositeTestAgent.GetAgent();
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var transactionBridgeApi = new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters);

			dynamic payload = transactionBridgeApi.CreateDistributedTracePayload();

			NrAssert.Multiple(
				() => Assert.IsEmpty(payload.HttpSafe()),
				() => Assert.IsEmpty(payload.Text()),
				() => Assert.IsTrue(payload.IsEmpty())
			);
		}

		[Test]
		public void ShouldCreateDistributedTracePayload()
		{
			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			var agentWrapperApi = _compositeTestAgent.GetAgent();
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var transactionBridgeApi = new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters);

			dynamic payload = transactionBridgeApi.CreateDistributedTracePayload();

			NrAssert.Multiple(
				() => Assert.IsNotEmpty(payload.HttpSafe()),
				() => Assert.IsNotEmpty(payload.Text()),
				() => Assert.IsFalse(payload.IsEmpty())
			);
		}

		[Test]
		public void ShouldAcceptStringDistributedTracePayloadWhenDTEnabled()
		{
			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			var agentWrapperApi = _compositeTestAgent.GetAgent();
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var transactionBridgeApi = new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters);

			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			transactionBridgeApi.AcceptDistributedTracePayload(_distributedTracePayload.ToJson(), 0 /*Unknown TransportType see Agent\NewRelic.Api.Agent\TransportType.cs for more info*/);

			segment.End();
			transaction.End();

			_compositeTestAgent.Harvest();

			var transactionEvent = _compositeTestAgent.TransactionEvents.First();
			var transactionAttributes = transactionEvent.IntrinsicAttributes;

			NrAssert.Multiple(
				() => Assert.AreEqual(_traceId, transactionAttributes["traceId"]),
				() => Assert.AreEqual(_type, transactionAttributes["parent.type"]),
				() => Assert.AreEqual(_appId, transactionAttributes["parent.app"]),
				() => Assert.AreEqual(_accountId, transactionAttributes["parent.account"]),
				() => Assert.AreEqual("Unknown", transactionAttributes["parent.transportType"]),
				() => Assert.True(transactionAttributes.ContainsKey("parent.transportDuration")),
				() => Assert.AreEqual(_transactionId, transactionAttributes["parentId"]),
				() => Assert.AreEqual(_priority, transactionAttributes["priority"]),
				() => Assert.AreEqual(_sampled, transactionAttributes["sampled"])
			);
		}

		[Test]
		public void ShouldNotAcceptStringDistributedTracePayloadWhenDTNotEnabled()
		{
			var agentWrapperApi = _compositeTestAgent.GetAgent();
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var transactionBridgeApi = new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters);

			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			transactionBridgeApi.AcceptDistributedTracePayload(_distributedTracePayload.ToJson(), 0 /*Unknown TransportType see Agent\NewRelic.Api.Agent\TransportType.cs for more info*/);

			segment.End();
			transaction.End();

			_compositeTestAgent.Harvest();

			var transactionEvent = _compositeTestAgent.TransactionEvents.First();

			var expectedMissingAttributes = new[]
			{
				"traceId",
				"parent.type",
				"parent.app",
				"parent.account",
				"parent.transportType",
				"parent.TransportDuration",
				"parentId",
				"priority",
				"sampled"
			};

			TransactionEventAssertions.DoesNotHaveAttributes(expectedMissingAttributes, AttributeClassification.Intrinsics, transactionEvent);
		}

	
		[Test]
		public void ShouldNotAcceptEmptyStringDistributedTracePayload()
		{
			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			var agentWrapperApi = _compositeTestAgent.GetAgent();
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var transactionBridgeApi = new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters);

			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			transactionBridgeApi.AcceptDistributedTracePayload(_emptyDistributedTracePayloadString, 0 /*Unknown TransportType see Agent\NewRelic.Api.Agent\TransportType.cs for more info*/);

			segment.End();
			transaction.End();

			_compositeTestAgent.Harvest();

			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric {Name =  "Supportability/DistributedTrace/AcceptPayload/Ignored/Null", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
		}

		[Test]
		public void ShouldNotAcceptBadStringDistributedTracePayload()
		{
			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			var agentWrapperApi = _compositeTestAgent.GetAgent();
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var transactionBridgeApi = new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters);

			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			transactionBridgeApi.AcceptDistributedTracePayload(_badDistributedTracePayloadString, 0 /*Unknown TransportType see Agent\NewRelic.Api.Agent\TransportType.cs for more info*/);

			segment.End();
			transaction.End();

			_compositeTestAgent.Harvest();

			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric {Name =  "Supportability/DistributedTrace/AcceptPayload/ParseException", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
		}
	}
}