using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.DistributedTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
	[TestFixture]
	public class TransactionEventMakerTests
	{
		private TransactionEventMaker _transactionEventMaker;

		private static ITimerFactory _timerFactory;
		private IConfiguration _configuration;
		private IConfigurationService _configurationService;
		private TransactionAttributeMaker _transactionAttributeMaker;
		private ITransactionMetricNameMaker _transactionMetricNameMaker;
		private IErrorService _errorService;
		private IAttributeDefinitionService _attribDefSvc;
		private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

		[SetUp]
		public void SetUp()
		{
			_timerFactory = Mock.Create<ITimerFactory>();
			var attributeService = Mock.Create<IAttributeService>();
			Mock.Arrange(() => attributeService.FilterAttributes(Arg.IsAny<AttributeCollection>(), Arg.IsAny<AttributeDestinations>())).Returns<AttributeCollection, AttributeDestinations>((attrs, _) => attrs);
			_transactionEventMaker = new TransactionEventMaker(attributeService);

			_configuration = Mock.Create<IConfiguration>();
			_configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

			_transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
				.Returns(new TransactionMetricName("WebTransaction", "TransactionName"));

			_transactionAttributeMaker = new TransactionAttributeMaker(_configurationService);

			_errorService = new ErrorService(_configurationService);
			_attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
		}

		[Test]
		public void GetTransactionEvent_ReturnsSyntheticEvent()
		{
			// ARRANGE
			var transaction = BuildTestTransaction(isSynthetics: true);

			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), txStats);

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			Assert.NotNull(transactionEvent);
			Assert.IsTrue(transactionEvent.IsSynthetics());
		}


		[Test]
		public void GetTransactionEvent_ReturnsCorrectAttributes()
		{
			// ARRANGE
			var transaction = BuildTestTransaction(statusCode: 200, uri:"http://foo.com");
			transaction.TransactionMetadata.AddUserAttribute("foo", "bar");
			var errorData = MakeErrorData();
			transaction.TransactionMetadata.TransactionErrorState.AddCustomErrorData(errorData);
			
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), txStats);

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);
			var agentAttributes = transactionEvent.AgentAttributes.Keys.ToArray();

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(29, transactionEvent.IntrinsicAttributes.Count),
				() => Assert.AreEqual("Transaction", transactionEvent.IntrinsicAttributes["type"]),
				() => Assert.AreEqual(6, transactionEvent.AgentAttributes.Count),
				() => Assert.AreEqual("200", transactionEvent.AgentAttributes["response.status"]),
				() => Assert.AreEqual(200, transactionEvent.AgentAttributes["http.statusCode"]),
				() => Assert.AreEqual("http://foo.com", transactionEvent.AgentAttributes["request.uri"]),
				() => Assert.Contains("host.displayName", agentAttributes),
				() => Assert.AreEqual(2, transactionEvent.UserAttributes.Count),
				() => Assert.AreEqual("bar", transactionEvent.UserAttributes["foo"]),
				() => Assert.AreEqual("baz", transactionEvent.UserAttributes["fiz"])
			);
		}

		[Test]
		public void GetTransactionEvent_DoesNotReturnsSyntheticEvent()
		{
			// ARRANGE
			var transaction = BuildTestTransaction(isSynthetics:false);

			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), txStats);

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			Assert.NotNull(transactionEvent);
			Assert.IsFalse(transactionEvent.IsSynthetics());
		}

		[Test]
		public void GetTransactionEvent_ReturnsCorrectDistributedTraceAttributes()
		{
			// ARRANGE

			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var immutableTransaction = BuildTestImmutableTransaction(sampled: true, guid: "guid");

			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), txStats);

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(19, transactionEvent.IntrinsicAttributes.Count, "transactionEvent.IntrinsicAttributes.Count"),
				() => Assert.Contains("guid", transactionEvent.IntrinsicAttributes.Keys.ToArray(), "IntrinsicAttributes.Keys.Contains('guid')"),
				() => Assert.AreEqual(immutableTransaction.TracingState.Type.ToString(), transactionEvent.IntrinsicAttributes["parent.type"], "parent.type"),
				() => Assert.AreEqual(immutableTransaction.TracingState.AppId, transactionEvent.IntrinsicAttributes["parent.app"], "parent.app"),
				() => Assert.AreEqual(immutableTransaction.TracingState.AccountId, transactionEvent.IntrinsicAttributes["parent.account"], "parent.account"),
				() => Assert.AreEqual(EnumNameCache<TransportType>.GetName(immutableTransaction.TracingState.TransportType), transactionEvent.IntrinsicAttributes["parent.transportType"], "parent.transportType"),
				() => Assert.AreEqual(immutableTransaction.TracingState.TransportDuration.TotalSeconds, (double)transactionEvent.IntrinsicAttributes["parent.transportDuration"], 0.000001d, "parent.transportDuration"),
				() => Assert.AreEqual(immutableTransaction.TracingState.TransactionId, transactionEvent.IntrinsicAttributes["parentId"], "parentId"),
				() => Assert.AreEqual(immutableTransaction.TracingState.ParentId, transactionEvent.IntrinsicAttributes["parentSpanId"], "parentSpanId"),
				() => Assert.AreEqual(immutableTransaction.TraceId, transactionEvent.IntrinsicAttributes["traceId"], "traceId"),
				() => Assert.AreEqual(immutableTransaction.Priority, transactionEvent.IntrinsicAttributes["priority"], "priority"),
				() => Assert.AreEqual(immutableTransaction.Sampled, transactionEvent.IntrinsicAttributes["sampled"], "sampled")
			);
		}

		private ErrorData MakeErrorData()
		{
			return new ErrorData("message", "type", "stacktrace", DateTime.UtcNow, new ReadOnlyDictionary<string, object>(new Dictionary<string, object>() { { "fiz", "baz" } }));
		}

		private const DistributedTracingParentType Type = DistributedTracingParentType.App;
		private const string AppId = "appId";
		private const string AccountId = "accountId";
		private const string GetTransportType = "HTTPS";
		private const string Guid = "guid";
		private static DateTime Timestamp = DateTime.UtcNow;
		private const string TraceId = "traceId";
		private const string TransactionId = "transactionId";
		private const bool Sampled = true;
		private const float Priority = 1.56f;
		private const string traceparentParentId = "parentId";

		private ImmutableTransaction BuildTestImmutableTransaction(bool isWebTransaction = true, string guid = null, float priority = 0.5f, bool sampled = false, string traceId = "traceId")
		{
			var name = TransactionName.ForWebTransaction("category", "name");

			var segments = Enumerable.Empty<Segment>();

			var placeholderMetadataBuilder = new TransactionMetadata();
			var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();

			var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.UtcNow, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), guid, false, false, false, priority, sampled, traceId, BuildMockTracingState(), _attribDefs);

			return immutableTransaction;
		}

		private IInternalTransaction BuildTestTransaction(bool isWebTransaction = true, string uri = null, string referrerUri = null, string guid = null, int? statusCode = null, int? subStatusCode = null, string referrerCrossProcessId = null, string transactionCategory = "defaultTxCategory", string transactionName = "defaultTxName", ErrorData exceptionData = null, ErrorData customErrorData = null, bool isSynthetics = true, bool isCAT = true, bool includeUserAttributes = false, float priority = 0.5f, bool sampled = false, string traceId = "traceId")
		{
			var name = isWebTransaction
				? TransactionName.ForWebTransaction(transactionCategory, transactionName)
				: TransactionName.ForOtherTransaction(transactionCategory, transactionName);

			var segments = Enumerable.Empty<Segment>();

			var placeholderMetadataBuilder = new TransactionMetadata();
			var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();

			var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.UtcNow, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), guid, false, false, false, priority, sampled, traceId, null, _attribDefs);

			var internalTransaction = new Transaction(Mock.Create<IConfiguration>(), immutableTransaction.TransactionName, _timerFactory.StartNewTimer(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), priority, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), _attribDefs);

			var adaptiveSampler = Mock.Create<IAdaptiveSampler>();
			Mock.Arrange(() => adaptiveSampler.ComputeSampled(ref priority)).Returns(sampled);
			internalTransaction.SetSampled(adaptiveSampler);

			var transactionMetadata = internalTransaction.TransactionMetadata;
			PopulateTransactionMetadataBuilder(transactionMetadata, uri, statusCode, subStatusCode, referrerCrossProcessId, exceptionData, customErrorData, isSynthetics, isCAT, referrerUri, includeUserAttributes);

			return internalTransaction;
		}

		private void PopulateTransactionMetadataBuilder(ITransactionMetadata metadata, string uri = null, int? statusCode = null, int? subStatusCode = null, string referrerCrossProcessId = null, ErrorData exceptionData = null, ErrorData customErrorData = null, bool isSynthetics = true, bool isCAT = true, string referrerUri = null, bool includeUserAttributes = false)
		{
			if (uri != null)
				metadata.SetUri(uri);
			if (statusCode != null)
				metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode, _errorService);
			if (referrerCrossProcessId != null)
				metadata.SetCrossApplicationReferrerProcessId(referrerCrossProcessId);
			if (statusCode != null)
				metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode, _errorService);
			if (exceptionData != null)
				metadata.TransactionErrorState.AddExceptionData((ErrorData)exceptionData);
			if (customErrorData != null)
				metadata.TransactionErrorState.AddCustomErrorData((ErrorData)customErrorData);
			if (referrerUri != null)
				metadata.SetReferrerUri(referrerUri);
			if (isCAT)
			{
				metadata.SetCrossApplicationReferrerProcessId("cross application process id");
				metadata.SetCrossApplicationReferrerTransactionGuid("transaction Guid");
			}

			metadata.SetQueueTime(TimeSpan.FromSeconds(10));
			metadata.SetOriginalUri("originalUri");
			metadata.SetCrossApplicationPathHash("crossApplicationPathHash");
			metadata.SetCrossApplicationReferrerContentLength(10000);
			metadata.SetCrossApplicationReferrerPathHash("crossApplicationReferrerPathHash");
			metadata.SetCrossApplicationReferrerTripId("crossApplicationReferrerTripId");

			if (includeUserAttributes)
			{
				metadata.AddUserAttribute("sample.user.attribute", "user attribute string");
			}

			if (isSynthetics)
			{
				metadata.SetSyntheticsResourceId("syntheticsResourceId");
				metadata.SetSyntheticsJobId("syntheticsJobId");
				metadata.SetSyntheticsMonitorId("syntheticsMonitorId");
			}
		}
		private static ITracingState BuildMockTracingState()
		{
			var tracingState = Mock.Create<ITracingState>();

			Mock.Arrange(() => tracingState.Type).Returns(Type);
			Mock.Arrange(() => tracingState.AppId).Returns(AppId);
			Mock.Arrange(() => tracingState.AccountId).Returns(AccountId);
			Mock.Arrange(() => tracingState.TransportType).Returns(TransportType.HTTP);
			Mock.Arrange(() => tracingState.Guid).Returns(Guid);
			Mock.Arrange(() => tracingState.Timestamp).Returns(Timestamp);
			Mock.Arrange(() => tracingState.TraceId).Returns(TraceId);
			Mock.Arrange(() => tracingState.TransactionId).Returns(TransactionId);
			Mock.Arrange(() => tracingState.Sampled).Returns(Sampled);
			Mock.Arrange(() => tracingState.Priority).Returns(Priority);
			Mock.Arrange(() => tracingState.ParentId).Returns(traceparentParentId);
			Mock.Arrange(() => tracingState.TraceContextWasAccepted).Returns(true);
			Mock.Arrange(() => tracingState.HasDataForParentAttributes).Returns(true);

			return tracingState;
		}
	}
}
