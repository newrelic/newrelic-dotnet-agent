using System;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
	[TestFixture]
	public class TransactionEventMakerTests
	{
		[NotNull] private TransactionEventMaker _transactionEventMaker;

		private static ITimerFactory _timerFactory;
		private IConfiguration _configuration;
		private IConfigurationService _configurationService;
		private TransactionAttributeMaker _transactionAttributeMaker;
		private ITransactionMetricNameMaker _transactionMetricNameMaker;


		[SetUp]
		public void SetUp()
		{
			_timerFactory = Mock.Create<ITimerFactory>();
			var attributeService = Mock.Create<IAttributeService>();
			Mock.Arrange(() => attributeService.FilterAttributes(Arg.IsAny<Attributes>(), Arg.IsAny<AttributeDestinations>())).Returns<Attributes, AttributeDestinations>((attrs, _) => attrs);
			_transactionEventMaker = new TransactionEventMaker(attributeService);

			_configuration = Mock.Create<IConfiguration>();
			_configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

			_transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
				.Returns(new TransactionMetricName("WebTransaction", "TransactionName"));

			_transactionAttributeMaker = new TransactionAttributeMaker(_configurationService);

		}

		[Test]
		public void GetTransactionEvent_ReturnsSyntheticEvent()
		{
			// ARRANGE
			var transaction = BuildTestTransaction(isSynthetics: true);

			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), errorData, txStats);

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			Assert.NotNull(transactionEvent);
			Assert.IsTrue(transactionEvent.IsSynthetics());
		}

		private static IInternalTransaction BuildTestTransaction(Boolean isWebTransaction = true, String uri = null, String referrerUri = null, String guid = null, Int32? statusCode = null, Int32? subStatusCode = null, String referrerCrossProcessId = null, String transactionCategory = "defaultTxCategory", String transactionName = "defaultTxName", ErrorData? exceptionData = null, ErrorData? customErrorData = null, Boolean isSynthetics = true, Boolean isCAT = true, Boolean includeUserAttributes = false)
		{
			var name = isWebTransaction
				? TransactionName.ForWebTransaction(transactionCategory, transactionName)
				: TransactionName.ForOtherTransaction(transactionCategory, transactionName);

			var segments = Enumerable.Empty<Segment>();

			var placeholderMetadataBuilder = new TransactionMetadata();
			var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();


			var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.Now, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), guid, false, false, false, SqlObfuscator.GetObfuscatingSqlObfuscator());

			var priority = 0.5f;
			var internalTransaction = new Transaction(Mock.Create<IConfiguration>(), immutableTransaction.TransactionName, _timerFactory.StartNewTimer(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), priority, Mock.Create<IDatabaseStatementParser>());
			var transactionMetadata = internalTransaction.TransactionMetadata;
			PopulateTransactionMetadataBuilder(transactionMetadata, uri, statusCode, subStatusCode, referrerCrossProcessId, exceptionData, customErrorData, isSynthetics, isCAT, referrerUri, includeUserAttributes);

			return internalTransaction;
		}

		private static void PopulateTransactionMetadataBuilder([NotNull] ITransactionMetadata metadata, String uri = null, Int32? statusCode = null, Int32? subStatusCode = null, String referrerCrossProcessId = null, ErrorData? exceptionData = null, ErrorData? customErrorData = null, Boolean isSynthetics = true, Boolean isCAT = true, String referrerUri = null, Boolean includeUserAttributes = false)
		{
			if (uri != null)
				metadata.SetUri(uri);
			if (statusCode != null)
				metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode);
			if (referrerCrossProcessId != null)
				metadata.SetCrossApplicationReferrerProcessId(referrerCrossProcessId);
			if (statusCode != null)
				metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode);
			if (exceptionData != null)
				metadata.AddExceptionData((ErrorData)exceptionData);
			if (customErrorData != null)
				metadata.AddCustomErrorData((ErrorData)customErrorData);
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

		[Test]
		public void GetTransactionEvent_ReturnsCorrectAttributes()
		{
			// ARRANGE
			var transaction = BuildTestTransaction(statusCode: 200, uri:"http://foo.com");
			transaction.TransactionMetadata.AddUserAttribute("foo", "bar");
			transaction.TransactionMetadata.AddUserErrorAttribute( "fiz", "baz");
			
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), errorData, txStats);

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);
			var agentAttributes = transactionEvent.AgentAttributes.Keys.ToArray();

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(24, transactionEvent.IntrinsicAttributes.Count),
				() => Assert.AreEqual("Transaction", transactionEvent.IntrinsicAttributes["type"]),
				() => Assert.AreEqual(5, transactionEvent.AgentAttributes.Count),
				() => Assert.AreEqual("200", transactionEvent.AgentAttributes["response.status"]),
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
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), errorData, txStats);

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
			var transaction = BuildTestTransaction(isSynthetics: false, isCAT:false, statusCode: 200, uri: "http://foo.com");

			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			transaction.TransactionMetadata.AddUserAttribute("foo", "bar");
			transaction.TransactionMetadata.AddUserErrorAttribute("fiz", "baz");
			transaction.TransactionMetadata.HasIncomingDistributedTracePayload = true;
			transaction.TransactionMetadata.DistributedTraceAccountId = "273070";
			transaction.TransactionMetadata.DistributedTraceAppId = "217958";
			transaction.TransactionMetadata.SetDistributedTraceTransportType(TransportType.HTTP);
			transaction.TransactionMetadata.DistributedTraceTransportDuration = new TimeSpan(0, 0, 5);
			transaction.TransactionMetadata.DistributedTraceGuid = "squid";
			transaction.TransactionMetadata.DistributedTraceTransactionId = "parentid";
			transaction.TransactionMetadata.Priority = .3f;
			transaction.TransactionMetadata.DistributedTraceSampled = true;
			transaction.TransactionMetadata.DistributedTraceTraceId = "traceid";
			transaction.TransactionMetadata.DistributedTraceType = "Mobile";

			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), errorData, txStats);

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(20, transactionEvent.IntrinsicAttributes.Count),
				() => Assert.Contains("guid", transactionEvent.IntrinsicAttributes.Keys.ToArray()),
				() => Assert.AreEqual(transaction.TransactionMetadata.DistributedTraceType, transactionEvent.IntrinsicAttributes["parent.type"]),
				() => Assert.AreEqual(transaction.TransactionMetadata.DistributedTraceAppId, transactionEvent.IntrinsicAttributes["parent.app"]),
				() => Assert.AreEqual(transaction.TransactionMetadata.DistributedTraceAccountId, transactionEvent.IntrinsicAttributes["parent.account"]),
				() => Assert.AreEqual(transaction.TransactionMetadata.DistributedTraceTransportType, transactionEvent.IntrinsicAttributes["parent.transportType"]),
				() => Assert.AreEqual(transaction.TransactionMetadata.DistributedTraceTransportDuration.TotalSeconds, transactionEvent.IntrinsicAttributes["parent.transportDuration"]),
				() => Assert.AreEqual(transaction.TransactionMetadata.DistributedTraceTransactionId, transactionEvent.IntrinsicAttributes["parentId"]),
				() => Assert.AreEqual(transaction.TransactionMetadata.DistributedTraceGuid, transactionEvent.IntrinsicAttributes["parentSpanId"]),
				() => Assert.AreEqual(transaction.TransactionMetadata.DistributedTraceTraceId, transactionEvent.IntrinsicAttributes["traceId"]),
				() => Assert.AreEqual(transaction.TransactionMetadata.Priority, transactionEvent.IntrinsicAttributes["priority"]),
				() => Assert.AreEqual(transaction.TransactionMetadata.DistributedTraceSampled, transactionEvent.IntrinsicAttributes["sampled"])
			);
		}
	}
}