using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class ErrorEventMakerTests
	{
		private IConfiguration _configuration;
		private IConfigurationService _configurationService;
		private IErrorEventMaker _errorEventMaker;
		private IErrorTraceMaker _errorTraceMaker;
		private ITransactionTransformer _transactionTransformer;
		private ITransactionMetricNameMaker _transactionMetricNameMaker;
		private ISegmentTreeMaker _segmentTreeMaker;
		private IMetricBuilder _metricBuilder;
		private IMetricNameService _metricNameService;
		private IMetricAggregator _metricAggregator;
		private ITransactionTraceAggregator _transactionTraceAggregator;
		private ITransactionTraceMaker _transactionTraceMaker;
		private ITransactionEventAggregator _transactionEventAggregator;
		private ITransactionEventMaker _transactionEventMaker;
		private ITransactionAttributeMaker _transactionAttributeMaker;
		private IErrorTraceAggregator _errorTraceAggregator;
		private IErrorEventAggregator _errorEventAggregator;
		private ISqlTraceAggregator _sqlTraceAggregator;
		private ISqlTraceMaker _sqlTraceMaker;
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
			_errorTraceMaker = new ErrorTraceMaker(_configurationService, attributeService);
			_errorEventMaker = new ErrorEventMaker(attributeService);

			_timerFactory = new TimerFactory();

			_metricBuilder = Mock.Create<IMetricBuilder>();
			_metricNameService = Mock.Create<IMetricNameService>();
			_metricAggregator = Mock.Create<IMetricAggregator>();
			_transactionTraceAggregator = Mock.Create<ITransactionTraceAggregator>();
			_transactionTraceMaker = Mock.Create<ITransactionTraceMaker>();
			_transactionEventAggregator = Mock.Create<ITransactionEventAggregator>();
			_transactionEventMaker = Mock.Create<ITransactionEventMaker>();
			_transactionAttributeMaker = new TransactionAttributeMaker();
			_errorTraceAggregator = Mock.Create<IErrorTraceAggregator>();
			_errorTraceMaker = Mock.Create<IErrorTraceMaker>();
			_errorEventAggregator = Mock.Create<IErrorEventAggregator>();
			_sqlTraceAggregator = Mock.Create<ISqlTraceAggregator>();
			_sqlTraceMaker = Mock.Create<ISqlTraceMaker>();

			_transactionTransformer = new TransactionTransformer(_transactionMetricNameMaker, 
				_segmentTreeMaker, _metricNameService, _metricAggregator, _configurationService,
				_transactionTraceAggregator, _transactionTraceMaker, _transactionEventAggregator, _transactionEventMaker,
				_transactionAttributeMaker, _errorTraceAggregator, _errorTraceMaker, _errorEventAggregator, _errorEventMaker,
				_sqlTraceAggregator, _sqlTraceMaker);

		}

		[Test]
		public void GetErrorEvent_InTransaction_IfStatusCodeIs404_ContainsCorrectAttributes()
		{
			var transaction = BuildTestTransaction(statusCode: 404, uri: "http://www.newrelic.com/test?param=value", isSynthetics: false, isCAT: false, referrerUri: "http://referrer.uri");
			var immutableTransaction = transaction.ConvertToImmutableTransaction();

			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			var txStats = new TransactionMetricStatsCollection(transactionMetricName);

			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), errorData, txStats);

			var errorEvent = _errorEventMaker.GetErrorEvent(errorData, immutableTransaction, attributes);

			var intrinsicAttributes = errorEvent.IntrinsicAttributes.Keys.ToArray();
			var agentAttributes = errorEvent.AgentAttributes.Keys.ToArray();
			var userAttributes = errorEvent.UserAttributes.Keys.ToArray();

			NrAssert.Multiple(
				() => Assert.AreEqual(false, errorEvent.IsSynthetics()),
				() => Assert.AreEqual(5, agentAttributes.Length),
				() => Assert.AreEqual(7, intrinsicAttributes.Length),
				() => Assert.AreEqual(0, userAttributes.Length),

				() => Assert.Contains("queue_wait_time_ms", agentAttributes),
				() => Assert.Contains("response.status", agentAttributes),
				() => Assert.Contains("original_url", agentAttributes),
				() => Assert.Contains("request_uri", agentAttributes),
				() => Assert.Contains("request.referer", agentAttributes),

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
				() => Assert.AreEqual(5, agentAttributes.Length),
				() => Assert.AreEqual(7, intrinsicAttributes.Length),
				() => Assert.AreEqual(0, userAttributes.Length),

				() => Assert.Contains("queue_wait_time_ms", agentAttributes),
				() => Assert.Contains("response.status", agentAttributes),
				() => Assert.Contains("original_url", agentAttributes),
				() => Assert.Contains("request_uri", agentAttributes),
				() => Assert.Contains("request.referer", agentAttributes),

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

				() => Assert.AreEqual(5, agentAttributes.Length),
				() => Assert.AreEqual(16, intrinsicAttributes.Length),
				() => Assert.AreEqual(1, userAttributes.Length),

				() => Assert.Contains("queue_wait_time_ms", agentAttributes),
				() => Assert.Contains("response.status", agentAttributes),
				() => Assert.Contains("original_url", agentAttributes),
				() => Assert.Contains("request_uri", agentAttributes),
				() => Assert.Contains("request.referer", agentAttributes),

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
			var customAttributes = new Attributes();

			customAttributes.Add(Attribute.BuildCustomAttribute("custom attribute name", "custom attribute value"));
			var errorData = ErrorData.FromException(new System.NullReferenceException("NRE message"), false);

			// Act
			var errorEvent = _errorEventMaker.GetErrorEvent(errorData, customAttributes);

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
			attributes.Add(Attribute.BuildDatabaseDurationAttribute((Single)TimeSpan.FromSeconds(10).TotalSeconds));
			attributes.Add(Attribute.BuildExternalCallCountAttribute(10));
			attributes.Add(Attribute.BuildExternalDurationAttribute((Single)TimeSpan.FromSeconds(10).TotalSeconds));

			return attributes.ToArray();
		}

		[NotNull]
		private static ITransaction BuildTestTransaction(Boolean isWebTransaction = true, String uri = null, String referrerUri = null, String guid = null, Int32? statusCode = null, Int32? subStatusCode = null, String referrerCrossProcessId = null, String transactionCategory = "defaultTxCategory", String transactionName = "defaultTxName", ErrorData? exceptionData = null, ErrorData? customErrorData = null, Boolean isSynthetics = true, Boolean isCAT = true, Boolean includeUserAttributes = false)
		{
			var name = isWebTransaction
				? new WebTransactionName(transactionCategory, transactionName)
				: new OtherTransactionName(transactionCategory, transactionName) as ITransactionName;
			var segments = Enumerable.Empty<Segment>();

			var placeholderMetadataBuilder = new TransactionMetadata();
			var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();


			var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.Now, TimeSpan.FromSeconds(10), guid, false, false, false, SqlObfuscator.GetObfuscatingSqlObfuscator());
			var internalTransaction = new Transaction(Mock.Create<IConfiguration>(), immutableTransaction.TransactionName, _timerFactory.StartNewTimer(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
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
			metadata.SetPath("path");
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

		[NotNull]
		private static ImmutableSegmentTreeNode BuildNode(TimeSpan relativeStart = new TimeSpan(), TimeSpan? duration = null)
		{
			var methodCallData = new MethodCallData("typeName", "methodName", 1);
			return new SegmentTreeNodeBuilder(
				new TypedSegment<SimpleSegmentData>(relativeStart, duration ?? TimeSpan.Zero, 
				new TypedSegment<SimpleSegmentData>(Mock.Create<ITransactionSegmentState>(), methodCallData, new SimpleSegmentData(""), false)))
				.Build();
		}

	}
}
