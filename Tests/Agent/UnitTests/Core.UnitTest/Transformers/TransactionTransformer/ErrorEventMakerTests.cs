using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using Attribute = NewRelic.Agent.Core.Attributes.Attribute;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class ErrorEventMakerTests
	{
		private IConfiguration _configuration;
		private IConfigurationService _configurationService;
		private IErrorEventMaker _errorEventMaker;
		private ITransactionMetricNameMaker _transactionMetricNameMaker;
		private ISegmentTreeMaker _segmentTreeMaker;
		private ITransactionAttributeMaker _transactionAttributeMaker;
		private static ITimerFactory _timerFactory;

		[SetUp]
		public void SetUp()
		{
			_configuration = Mock.Create<IConfiguration>();

			Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(true);
			Mock.Arrange(() => _configuration.ErrorCollectorCaptureEvents).Returns(true);

			_configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

			_transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
				.Returns(new TransactionMetricName("WebTransaction", "TransactionName"));

			_segmentTreeMaker = Mock.Create<ISegmentTreeMaker>();
			Mock.Arrange(() => _segmentTreeMaker.BuildSegmentTrees(Arg.IsAny<IEnumerable<Segment>>()))
				.Returns(new[] { BuildNode() });

			var attributeService = new AttributeService();
			_errorEventMaker = new ErrorEventMaker(attributeService);

			_timerFactory = new TimerFactory();

			_transactionAttributeMaker = new TransactionAttributeMaker(_configurationService);
		}

		[Test]
		public void GetErrorEvent_InTransaction_IfStatusCodeIs404_ContainsCorrectAttributes()
		{
			var transaction = BuildTestTransaction(statusCode: 404, uri: "http://www.newrelic.com/test?param=value", isSynthetics: false, isCAT: false, referrerUri: "http://referrer.uri");
			var immutableTransaction = transaction.ConvertToImmutableTransaction();

			var errorData = ErrorData.TryGetErrorData(immutableTransaction, Enumerable.Empty<string>(), Enumerable.Empty<string>());
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);

			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), errorData, txStats);

			var errorEvent = _errorEventMaker.GetErrorEvent(errorData, immutableTransaction, attributes);

			var intrinsicAttributes = errorEvent.IntrinsicAttributes.Keys.ToArray();
			var agentAttributes = errorEvent.AgentAttributes.Keys.ToArray();
			var userAttributes = errorEvent.UserAttributes.Keys.ToArray();

			NrAssert.Multiple(
				() => Assert.AreEqual(false, errorEvent.IsSynthetics()),
				() => Assert.AreEqual(7, agentAttributes.Length),
				() => Assert.AreEqual(7, intrinsicAttributes.Length),
				() => Assert.AreEqual(0, userAttributes.Length),

				() => Assert.Contains("queue_wait_time_ms", agentAttributes),
				() => Assert.Contains("response.status", agentAttributes),
				() => Assert.Contains("http.statusCode", agentAttributes),
				() => Assert.Contains("original_url", agentAttributes),
				() => Assert.Contains("request.uri", agentAttributes),
				() => Assert.Contains("request.referer", agentAttributes),
				() => Assert.Contains("host.displayName", agentAttributes),

				() => Assert.Contains("duration", intrinsicAttributes),
				() => Assert.Contains("error.class", intrinsicAttributes),
				() => Assert.Contains("error.message", intrinsicAttributes),
				() => Assert.Contains("queueDuration", intrinsicAttributes),
				() => Assert.Contains("transactionName", intrinsicAttributes),
				() => Assert.Contains("timestamp", intrinsicAttributes),
				() => Assert.Contains("type", intrinsicAttributes)
			);
		}

		[Test]
		public void GetErrorEvent_InTransaction_WithException_ContainsCorrectAttributes()
		{
			var error = new OutOfMemoryException("Out of Memory Message");
			var errorData = ErrorData.FromParts(error.Message, "OutOfMemoryError", DateTime.UtcNow, false);

			var transaction = BuildTestTransaction(statusCode: 404, uri: "http://www.newrelic.com/test?param=value", isSynthetics: false, isCAT: false, referrerUri: "http://referrer.uri");
			var immutableTransaction = transaction.ConvertToImmutableTransaction();

			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);

			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(15), errorData, txStats);

			var errorEvent = _errorEventMaker.GetErrorEvent(errorData, immutableTransaction, attributes);

			var intrinsicAttributes = errorEvent.IntrinsicAttributes.Keys.ToArray();
			var agentAttributes = errorEvent.AgentAttributes.Keys.ToArray();
			var userAttributes = errorEvent.UserAttributes.Keys.ToArray();

			NrAssert.Multiple(
				() => Assert.AreEqual(false, errorEvent.IsSynthetics()),
				() => Assert.AreEqual(7, agentAttributes.Length),
				() => Assert.AreEqual(7, intrinsicAttributes.Length),
				() => Assert.AreEqual(0, userAttributes.Length),

				() => Assert.Contains("queue_wait_time_ms", agentAttributes),
				() => Assert.Contains("response.status", agentAttributes),
				() => Assert.Contains("original_url", agentAttributes),
				() => Assert.Contains("request.uri", agentAttributes),
				() => Assert.Contains("http.statusCode", agentAttributes),
				() => Assert.Contains("request.referer", agentAttributes),
				() => Assert.Contains("host.displayName", agentAttributes),

				() => Assert.Contains("duration", intrinsicAttributes),
				() => Assert.Contains("error.class", intrinsicAttributes),
				() => Assert.Contains("error.message", intrinsicAttributes),
				() => Assert.Contains("queueDuration", intrinsicAttributes),
				() => Assert.Contains("transactionName", intrinsicAttributes),
				() => Assert.Contains("timestamp", intrinsicAttributes),
				() => Assert.Contains("type", intrinsicAttributes)
			);
		}

		[Test]
		public void GetErrorEvent_InTransaction_WithException_ContainsCorrectAttributes_FullAttributes()
		{
			var error = new OutOfMemoryException("Out of Memory Message");
			var errorData = ErrorData.FromParts(error.Message, "OutOfMemoryError", DateTime.UtcNow, false);

			var transaction = BuildTestTransaction(statusCode: 404,
															customErrorData: errorData,
															uri: "http://www.newrelic.com/test?param=value",
															referrerUri: "http://referrer.uri",
															includeUserAttributes: true);

			var immutableTransaction = transaction.ConvertToImmutableTransaction();

			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);

			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(15), errorData, txStats);

			attributes.Add(GetIntrinsicAttributes());

			var errorEvent = _errorEventMaker.GetErrorEvent(errorData, immutableTransaction, attributes);

			var intrinsicAttributes = errorEvent.IntrinsicAttributes.Keys.ToArray();
			var agentAttributes = errorEvent.AgentAttributes.Keys.ToArray();
			var userAttributes = errorEvent.UserAttributes.Keys.ToArray();

			NrAssert.Multiple(
				() => Assert.AreEqual(true, errorEvent.IsSynthetics()),

				() => Assert.AreEqual(7, agentAttributes.Length),
				() => Assert.AreEqual(16, intrinsicAttributes.Length),
				() => Assert.AreEqual(1, userAttributes.Length),

				() => Assert.Contains("queue_wait_time_ms", agentAttributes),
				() => Assert.Contains("response.status", agentAttributes),
				() => Assert.Contains("http.statusCode", agentAttributes),
				() => Assert.Contains("original_url", agentAttributes),
				() => Assert.Contains("request.uri", agentAttributes),
				() => Assert.Contains("request.referer", agentAttributes),
				() => Assert.Contains("host.displayName", agentAttributes),

				() => Assert.Contains("duration", intrinsicAttributes),
				() => Assert.Contains("error.class", intrinsicAttributes),
				() => Assert.Contains("error.message", intrinsicAttributes),
				() => Assert.Contains("queueDuration", intrinsicAttributes),
				() => Assert.Contains("transactionName", intrinsicAttributes),
				() => Assert.Contains("timestamp", intrinsicAttributes),
				() => Assert.Contains("type", intrinsicAttributes),
				() => Assert.Contains("nr.syntheticsJobId", intrinsicAttributes),
				() => Assert.Contains("nr.syntheticsResourceId", intrinsicAttributes),
				() => Assert.Contains("nr.syntheticsMonitorId", intrinsicAttributes),
				() => Assert.Contains("nr.referringTransactionGuid", intrinsicAttributes),
				() => Assert.Contains("databaseDuration", intrinsicAttributes),
				() => Assert.Contains("databaseCallCount", intrinsicAttributes),
				() => Assert.Contains("externalDuration", intrinsicAttributes),
				() => Assert.Contains("externalCallCount", intrinsicAttributes),
				() => Assert.Contains("nr.guid", intrinsicAttributes),

				() => Assert.Contains("sample.user.attribute", userAttributes)
			);
		}

		[Test]
		public void GetErrorEvent_NoTransaction_WithException_ContainsCorrectAttributes()
		{
			// Arrange
			var customAttributes = new AttributeCollection();

			customAttributes.Add(Attribute.BuildCustomAttribute("custom attribute name", "custom attribute value"));
			var errorData = ErrorData.FromException(new System.NullReferenceException("NRE message"), false);

			// Act
			float priority = 0.5f;
			var errorEvent = _errorEventMaker.GetErrorEvent(errorData, customAttributes, priority);

			var agentAttributes = errorEvent.AgentAttributes.Keys.ToArray();
			var intrinsicAttributes = errorEvent.IntrinsicAttributes.Keys.ToArray();
			var userAttributes = errorEvent.UserAttributes.Keys.ToArray();

			// Assert
			NrAssert.Multiple(
				() => Assert.AreEqual(false, errorEvent.IsSynthetics()),
				() => Assert.AreEqual(0, agentAttributes.Length),
				() => Assert.AreEqual(4, intrinsicAttributes.Length),
				() => Assert.AreEqual(1, userAttributes.Length),

				() => Assert.Contains("error.class", intrinsicAttributes),
				() => Assert.Contains("error.message", intrinsicAttributes),
				() => Assert.Contains("timestamp", intrinsicAttributes),
				() => Assert.Contains("type", intrinsicAttributes),

				() => Assert.Contains("custom attribute name", userAttributes)
			);
		}

		private static Attribute[] GetIntrinsicAttributes()
		{
			var attributes = new List<Attribute>();
	
			attributes.Add(Attribute.BuildDatabaseCallCountAttribute(10));
			attributes.Add(Attribute.BuildDatabaseDurationAttribute((float)TimeSpan.FromSeconds(10).TotalSeconds));
			attributes.Add(Attribute.BuildExternalCallCountAttribute(10));
			attributes.Add(Attribute.BuildExternalDurationAttribute((float)TimeSpan.FromSeconds(10).TotalSeconds));

			return attributes.ToArray();
		}

		private static IInternalTransaction BuildTestTransaction(bool isWebTransaction = true, string uri = null, string referrerUri = null, string guid = null, int? statusCode = null, int? subStatusCode = null, string referrerCrossProcessId = null, string transactionCategory = "defaultTxCategory", string transactionName = "defaultTxName", ErrorData? exceptionData = null, ErrorData? customErrorData = null, bool isSynthetics = true, bool isCAT = true, bool includeUserAttributes = false)
		{
			var name = isWebTransaction
				? TransactionName.ForWebTransaction(transactionCategory, transactionName)
				: TransactionName.ForOtherTransaction(transactionCategory, transactionName);

			var segments = Enumerable.Empty<Segment>();

			var placeholderMetadataBuilder = new TransactionMetadata();
			var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();


			var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.Now, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), guid, false, false, false);

			var priority = 0.5f;
			var internalTransaction = new Transaction(Mock.Create<IConfiguration>(), immutableTransaction.TransactionName, _timerFactory.StartNewTimer(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), priority, Mock.Create<IDatabaseStatementParser>());
			var transactionMetadata = internalTransaction.TransactionMetadata;
			PopulateTransactionMetadataBuilder(transactionMetadata, uri, statusCode, subStatusCode, referrerCrossProcessId, exceptionData, customErrorData, isSynthetics, isCAT, referrerUri, includeUserAttributes);

			return internalTransaction;
		}

		private static void PopulateTransactionMetadataBuilder(ITransactionMetadata metadata, string uri = null, int? statusCode = null, int? subStatusCode = null, string referrerCrossProcessId = null, ErrorData? exceptionData = null, ErrorData? customErrorData = null, bool isSynthetics = true, bool isCAT = true, string referrerUri = null, bool includeUserAttributes = false)
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

		private static ImmutableSegmentTreeNode BuildNode(TimeSpan relativeStart = new TimeSpan(), TimeSpan? duration = null)
		{
			var methodCallData = new MethodCallData("typeName", "methodName", 1);
			var segment = new Segment(Mock.Create<ITransactionSegmentState>(), methodCallData);
			segment.SetSegmentData(new SimpleSegmentData(""));

			return new SegmentTreeNodeBuilder(
				new Segment(relativeStart, duration ?? TimeSpan.Zero, segment, null))
				.Build();
		}

	}
}
