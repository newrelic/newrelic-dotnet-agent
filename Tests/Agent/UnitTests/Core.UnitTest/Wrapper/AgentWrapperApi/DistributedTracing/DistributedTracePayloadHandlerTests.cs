using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing
{
	[TestFixture]
	public class DistributedTracePayloadHandlerTests
	{
		private const string DistributedTraceHeaderName = "Newrelic";
		private const string TracestateHeaderName = "tracestate";
		private const string TraceParentHeaderName = "traceparent";


		private const DistributedTracingParentType DtTypeApp = DistributedTracingParentType.App;
		private const DistributedTracingParentType IncomingDtType = DistributedTracingParentType.Mobile;
		private const string AgentAccountId = "273070";
		private const string IncomingAccountId = "222222";
		private const string AgentApplicationId = "238575";
		private const string IncomingApplicationId = "888888";
		private const string IncomingDtGuid = "6d10a3dfbc4448a1";
		private const string IncomingDtTraceId = "e6463956f2c14ddaa3f737104381714f";
		private const string IncomingTrustKey = "12345";
		private const float Priority = 0.5f;
		private const float IncomingPriority = 0.75f;
		private const string TransactionId = "b85cf40e58084c21";

		private DistributedTracePayloadHandler _distributedTracePayloadHandler;
		private IConfiguration _configuration;
		private IAdaptiveSampler _adaptiveSampler;
		private IAgentHealthReporter _agentHealthReporter;

		[SetUp]
		public void SetUp()
		{
			_configuration = Mock.Create<IConfiguration>();
			_adaptiveSampler = Mock.Create<IAdaptiveSampler>();

			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(true);
			Mock.Arrange(() => _configuration.AccountId).Returns(AgentAccountId);
			Mock.Arrange(() => _configuration.PrimaryApplicationId).Returns(AgentApplicationId);
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(IncomingTrustKey);

			var configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);
			
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>();
			_distributedTracePayloadHandler = new DistributedTracePayloadHandler(configurationService, _agentHealthReporter, _adaptiveSampler);
		}

		#region Accept Incoming Request

		[Test]
		public void TryDecodeInboundSerializedDistributedTracePayload_ReturnsValidPayload()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.TransactionId = TransactionId;
			payload.Guid = null;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNotNull(decodedPayload);

			NrAssert.Multiple(
				() => Assert.AreEqual(IncomingDtType.ToString(), decodedPayload.Type),
				() => Assert.AreEqual(IncomingAccountId, decodedPayload.AccountId),
				() => Assert.AreEqual(AgentApplicationId, decodedPayload.AppId),
				() => Assert.AreEqual(null, decodedPayload.Guid),
				() => Assert.AreEqual(IncomingTrustKey, decodedPayload.TrustKey),
				() => Assert.AreEqual(IncomingPriority, decodedPayload.Priority),
				() => Assert.AreEqual(false, decodedPayload.Sampled),
				() => Assert.That(decodedPayload.Timestamp, Is.LessThan(DateTime.UtcNow)),
				() => Assert.AreEqual(TransactionId, decodedPayload.TransactionId)
			);
		}

		[Test]
		public void PayloadShouldBeNullWhenTrustKeyNotTrusted()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns("NOPE");

			var payload = BuildSampleDistributedTracePayload();
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);

			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNull(decodedPayload);
		}

		[Test]
		public void PayloadShouldBePopulatedWhenTrustKeyTrusted()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNotNull(decodedPayload);
		}

		[Test]
		public void PayloadShouldBeNullWhenTrustKeyNullAndAccountIdNotTrusted()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns("NOPE");

			var payload = BuildSampleDistributedTracePayload();
			payload.TrustKey = null;
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNull(decodedPayload);
		}

		[Test]
		public void PayloadShouldBePopulatedWhenTrustKeyNullAndAccountIdTrusted()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(IncomingAccountId);

			var payload = BuildSampleDistributedTracePayload();
			payload.TrustKey = null;
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNotNull(decodedPayload);
		}


		[Test]
		public void TryDecodeInboundSerializedDistributedTracePayload_ReturnsNull_IfHigherMajorVersion()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.Version = new[] {int.MaxValue, 1};
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNull(decodedPayload);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenGuidAndTransactionIdNull()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.Guid = null;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNull(decodedPayload);
		}

		[Test]
		public void ShouldGenerateParseExceptionMetricWhenGuidAndTransactionIdNull()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.Guid = null;
			payload.TransactionId = null;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			_distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException(), Occurs.Once());
		}


		#endregion Accept Incoming Request

		#region Create Outbound Request

		[Test]
		public void TryGetOutboundDistributedTraceApiModel_ReturnsCorrectModel_IfFirstInChain()
		{
			// Arrange
			var transaction = BuildMockTransaction(hasIncomingPayload: false);

			Mock.Arrange(() => transaction.TraceId).Returns((string)null);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();

			DistributedTracePayload dtPayload = null;
			Assert.That(() => dtPayload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson), Is.Not.Null);

			// Assert
			NrAssert.Multiple(
				() => Assert.AreEqual(DtTypeApp.ToString(), dtPayload.Type),
				() => Assert.AreEqual(AgentAccountId, dtPayload.AccountId),
				() => Assert.AreEqual(AgentApplicationId, dtPayload.AppId),
				() => Assert.AreEqual(null, dtPayload.Guid),
				() => Assert.AreEqual(transaction.Guid, dtPayload.TraceId),
				() => Assert.AreEqual(Priority, dtPayload.Priority),
				() => Assert.That(dtPayload.Timestamp, Is.LessThan(DateTime.UtcNow)),
				() => Assert.AreEqual($"{transaction.Guid}", dtPayload.TransactionId)
			);
		}

		[Test]
		public void TryGetOutboundDistributedTraceApiModel_ReturnsCorrectModel_IfNotFirstInChain()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(IncomingTrustKey);
			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();

			DistributedTracePayload dtPayload = null;
			Assert.That(() => dtPayload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson), Is.Not.Null);

			// Assert
			NrAssert.Multiple(
				() => Assert.AreEqual(DtTypeApp.ToString(), dtPayload.Type),
				() => Assert.AreEqual(AgentAccountId, dtPayload.AccountId),
				() => Assert.AreEqual(AgentApplicationId, dtPayload.AppId),
				() => Assert.AreEqual(null, dtPayload.Guid),
				() => Assert.AreEqual(IncomingDtTraceId, dtPayload.TraceId),
				() => Assert.AreEqual(IncomingTrustKey, dtPayload.TrustKey),
				() => Assert.AreEqual(IncomingPriority, dtPayload.Priority),
				() => Assert.That(dtPayload.Timestamp, Is.LessThan(DateTime.UtcNow)),
				() => Assert.AreEqual($"{transaction.Guid}", dtPayload.TransactionId)
			);
		}

		[Test]
		public void ShouldPopulateTrustKeyWhenTrustedAccountKeyDifferentThanAccountId()
		{
			// Arrange
			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload);
			Assert.AreEqual(IncomingTrustKey, payload.TrustKey);
		}

		[Test]
		public void ShouldNotPopulateTrustKeyWhenTrustedAccountKeySameAsAccountId()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(AgentAccountId);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload);
			Assert.IsNull(payload.TrustKey);
		}

		[Test]
		public void PayloadShouldHaveGuidWhenSpansEnabledAndTransactionSampled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

			var transaction = BuildMockTransaction(sampled: true);
			//transaction.TransactionMetadata.DistributedTraceSampled = true;

			const string expectedGuid = "expectedId";
			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(expectedGuid);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload.Guid);
			Assert.AreEqual(expectedGuid, payload.Guid);
		}

		[Test]
		public void PayloadShouldNotHaveGuidWhenSpansDisabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(false);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			const string expectedGuid = "expectedId";
			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(expectedGuid);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.IsNull(payload.Guid);
		}

		[Test]
		public void PayloadShouldNotHaveGuidWhenSpansEnabledAndTransactionNotSampled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

			var transaction = BuildMockTransaction(sampled: false);
			//transaction.TransactionMetadata.DistributedTraceSampled = false;

			const string expectedGuid = "expectedId";
			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(expectedGuid);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.IsNull(payload.Guid);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenAccountIdNotReceivedFromServer()
		{
			// Arrange
			Mock.Arrange(() => _configuration.AccountId).Returns<string>(null);
			Mock.Arrange(() => _configuration.PrimaryApplicationId).Returns(AgentApplicationId);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var payload = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

			// Assert
			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenPrimaryApplicationIdNotReceivedFromServer()
		{
			// Arrange
			Mock.Arrange(() => _configuration.AccountId).Returns(AgentAccountId);
			Mock.Arrange(() => _configuration.PrimaryApplicationId).Returns<string>(null);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var payload = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

			// Assert
			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenSampledNotSet()
		{
			// Arrange
			var transaction = BuildMockTransaction(sampled: null);
			//transaction.TransactionMetadata.DistributedTraceSampled = null;

			// Act
			var payload = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

			// Assert
			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}

		[Test]
		public void PayloadShouldNotHaveTransactionIdWhenTransactionEventsDisabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(false);

			var segment = Mock.Create<Segment>();
			Mock.Arrange(() => segment.SpanId).Returns("56789");

			var transaction = BuildMockTransaction(sampled: true);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.IsNull(payload.TransactionId);
		}

		[Test]
		public void PayloadShouldHaveTransactionIdWhenTransactionEventsEnabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(true);

			var transaction = BuildMockTransaction();

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload.TransactionId);
			Assert.AreEqual(transaction.Guid, payload.TransactionId);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenSpanEventsDisabledAndTransactionEventsDisabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(false);
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(false);

			var transaction = BuildMockTransaction();

			// Act
			var payload = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

			// Assert
			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}

		#region W3C Tests
		[TestCase("getHeaders is null")]
		[TestCase("getHeaders throws exception")]
		public void W3C_AcceptDistributedTraceHeaders_DoesNotThrowException(string testCaseName)
		{
			// Arrange
			var transaction = Mock.Create<ITransaction>();
			var headers = new List<KeyValuePair<string, string>>();

			Func<string, IList<string>> getHeaders = null;
			if (testCaseName != "getHeaders is null")
			{
				getHeaders = new Func<string, IList<string>> ((key) =>
				{
					throw new Exception("Exception occurred in getHeaders.");
				});
			}

			var tracingState = Mock.Create<ITracingState>();
			var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

			Mock.Arrange(() => transaction.AcceptDistributedTraceHeaders(Arg.IsAny<Func<string, IList<string>>>()
				,Arg.IsAny<TransportType>())).DoInstead(() => _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(getHeaders, TransportType.HTTP));

			// Act
			Assert.DoesNotThrow(() => transaction.AcceptDistributedTraceHeaders(getHeaders, TransportType.HTTP));
			Assert.That(headers.Count == 0);
		}

		[TestCase(true)]
		[TestCase(false)]
		public void W3C_InsertDistributedTraceHeaders_OutboundHeaders(bool hasIncomingPayload)
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

			var transaction = BuildMockTransaction(hasIncomingPayload: hasIncomingPayload, sampled: true);
			
			var transactionGuid = GuidGenerator.GenerateNewRelicGuid();
			Mock.Arrange(() => transaction.Guid).Returns(transactionGuid);

			var expectedSpanGuid = GuidGenerator.GenerateNewRelicGuid();
			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(expectedSpanGuid);

			Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

			var headers = new List<KeyValuePair<string, string>>();
			var setHeaders = new Action<string, string>((key, value) =>
			{
				headers.Add(new KeyValuePair<string, string>(key, value));
			});

			var tracingState = Mock.Create<ITracingState>();

			var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

			Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
			Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

			Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<Action<string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, setHeaders));

			// Act
			transaction.InsertDistributedTraceHeaders(setHeaders);

			Assert.That(headers.Where(header => header.Key == DistributedTraceHeaderName).Count() > 0, "There must be at least a newrelic header");
			Assert.That(headers.Where(header => header.Key == TraceParentHeaderName).Count() > 0, "There must be at least a traceparent header");
			Assert.That(headers.Where(header => header.Key == TracestateHeaderName).Count() > 0, "There must be at least a tracestate header");

			var tracestateHeaderValue = headers.Where(header => header.Key == TracestateHeaderName).Select(header => header.Value).ToList();
			var traceState = W3CTracestate.GetW3CTracestateFromHeaders(tracestateHeaderValue, IncomingTrustKey);
			Assert.NotNull(traceState);
			Assert.That(traceState.AccountId == AgentAccountId);
			Assert.That(traceState.AccountKey == IncomingTrustKey);
			Assert.That(traceState.AppId == AgentApplicationId);
			Assert.That(traceState.TransactionId == transactionGuid);
			Assert.That(traceState.ParentType == 0);
			Assert.That(traceState.Priority == (hasIncomingPayload ? IncomingPriority : Priority));
			Assert.That(traceState.SpanId == expectedSpanGuid);
			Assert.That(traceState.VendorstateEntries.SequenceEqual(vendorStateEntries));
			Assert.That(traceState.Version == 0);

			var traceParentHeaderValue = headers.Where(header => header.Key == TraceParentHeaderName).Select(header => header.Value).FirstOrDefault();

			var traceIdExpectedLength = 32;

			var tracepararent = W3CTraceparent.GetW3CTraceparentFromHeader(traceParentHeaderValue);

			var expectedTraceId = transaction.TraceId;
			expectedTraceId = expectedTraceId.PadLeft(traceIdExpectedLength, '0').ToLowerInvariant();
			var expectedParentId = expectedSpanGuid;

			Assert.That(tracepararent.TraceId.Length == traceIdExpectedLength);
			Assert.That(tracepararent.TraceId == expectedTraceId);
			Assert.That(tracepararent.Version == 0);
			Assert.That(tracepararent.ParentId == expectedParentId);
			Assert.That(tracepararent.TraceFlags == "01");

			var nrHeaderValue = headers.Where(header => header.Key == DistributedTraceHeaderName).Select(header => header.Value).FirstOrDefault();
			Assert.NotNull(nrHeaderValue);
		}

		[Test]
		public void W3C_InsertDistributedTraceHeaders_ExcludeNewRelicHeader()
		{
			// Arrange
			Mock.Arrange(() => _configuration.ExcludeNewrelicHeader).Returns(true);

			var transaction = BuildMockTransaction(hasIncomingPayload: true, sampled: true);

			Mock.Arrange(() => transaction.Guid).Returns(GuidGenerator.GenerateNewRelicGuid());

			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(GuidGenerator.GenerateNewRelicGuid());
			Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

			var headers = new List<KeyValuePair<string, string>>();
			var setHeaders = new Action<string, string>((key, value) =>
			{
				headers.Add(new KeyValuePair<string, string>(key, value));
			});

			var tracingState = Mock.Create<ITracingState>();
			var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

			Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
			Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

			Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<Action<string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, setHeaders));

			// Act
			transaction.InsertDistributedTraceHeaders(setHeaders);

			Assert.That(headers.Count == 2);
			Assert.That(headers.Where(header => header.Key == DistributedTraceHeaderName).Count() == 0, "There should not be a newrelic header");
			Assert.That(headers.Where(header => header.Key == TraceParentHeaderName).Count() > 0, "There must be at least a traceparent header");
			Assert.That(headers.Where(header => header.Key == TracestateHeaderName).Count() > 0, "There must be at least a tracestate header");
		}

		[TestCase(.1234567f,"0.123457")]
		[TestCase(1.123000f,"1.123")]
		public void W3C_InsertDistributedTraceHeaders_PriorityInRightFormat(float testPriority, string expectedPriorityString)
		{
			// Arrange
			Mock.Arrange(() => _configuration.ExcludeNewrelicHeader).Returns(true);

			var transaction = BuildMockTransaction(sampled: true);
			Mock.Arrange(() => transaction.Priority).Returns(testPriority);

			Mock.Arrange(() => transaction.Guid).Returns(GuidGenerator.GenerateNewRelicGuid());

			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(GuidGenerator.GenerateNewRelicGuid());
			Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

			var headers = new List<KeyValuePair<string, string>>();
			var setHeaders = new Action<string, string>((key, value) =>
			{
				headers.Add(new KeyValuePair<string, string>(key, value));
			});

			var tracingState = Mock.Create<ITracingState>();
			var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

			Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
			Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

			Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<Action<string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, setHeaders));

			// Act
			transaction.InsertDistributedTraceHeaders(setHeaders);

			var tracestateHeaderValue = headers.Where(header => header.Key == TracestateHeaderName).Select(header => header.Value).ToList();
			var priorityIndex = 7;
			var priorityString = tracestateHeaderValue[0].Split('-')[priorityIndex];
			Assert.AreEqual(expectedPriorityString, priorityString);
		}

		[TestCase("E6463956F2C14DDAA3F737104381714F", "e6463956f2c14ddaa3f737104381714f")]
		[TestCase("E6463956F2C14DDA", "0000000000000000e6463956f2c14dda")]
		[TestCase("e6463956f2c14dda", "0000000000000000e6463956f2c14dda")]
		public void W3C_InsertDistributedTraceHeaders_TraceIdInRightFormat(string testTraceId, string expectedTraceId)
		{
			// Arrange
			Mock.Arrange(() => _configuration.ExcludeNewrelicHeader).Returns(true);

			var transaction = BuildMockTransaction(hasIncomingPayload: true, sampled: true);

			Mock.Arrange(() => transaction.TraceId).Returns(testTraceId);

			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(GuidGenerator.GenerateNewRelicGuid());
			Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

			var headers = new List<KeyValuePair<string, string>>();
			var setHeaders = new Action<string, string>((key, value) =>
			{
				headers.Add(new KeyValuePair<string, string>(key, value));
			});

			var tracingState = Mock.Create<ITracingState>();
			var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

			Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
			Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

			Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<Action<string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, setHeaders));

			// Act
			transaction.InsertDistributedTraceHeaders(setHeaders);

			var traceParentHeaderValue = headers.Where(header => header.Key == TraceParentHeaderName).Select(header => header.Value).ToList();
			var traceIdIndex = 1;
			var traceId = traceParentHeaderValue[0].Split('-')[traceIdIndex];
			Assert.AreEqual(expectedTraceId, traceId);
		}

		[TestCase("setHeaders is null")]
		[TestCase("setHeaders throws exception")]
		public void W3C_InsertDistributedTraceHeaders_DoesNotThrowException(string testCaseName)
		{
			// Arrange
			var transaction = BuildMockTransaction(hasIncomingPayload: true, sampled: true);

			Mock.Arrange(() => transaction.Guid).Returns(GuidGenerator.GenerateNewRelicGuid());

			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(GuidGenerator.GenerateNewRelicGuid());
			Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

			var headers = new List<KeyValuePair<string, string>>();

			Action<string, string> setHeaders = null;
			if(testCaseName != "setHeaders is null") 
			{
				setHeaders = new Action<string, string>((key, value) =>
				{
					throw new Exception("Exception occurred in setHeaders.");
				});
			}

			var tracingState = Mock.Create<ITracingState>();
			var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

			Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
			Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

			Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<Action<string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, setHeaders));

			// Act
			Assert.DoesNotThrow(() => transaction.InsertDistributedTraceHeaders(setHeaders));
			Assert.That(headers.Count == 0);
		}

		#endregion W3C Tests

		#region TraceId Tests

		[Test]
		public void PayloadShouldHaveTraceIdWhenSpansEnabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);
			var segment = Mock.Create<ISegment>();

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload.TraceId);
		}

		[Test]
		public void PayloadShouldHaveTraceIdWhenSpansDisabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(false);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);
			var segment = Mock.Create<ISegment>();

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload.TraceId);
		}

		[Test]
		public void TraceIdShouldBeSameAsIncomingTraceIdWhenReceived()
		{
			var transaction = BuildMockTransaction(hasIncomingPayload: true);
			var segment = Mock.Create<ISegment>();

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.AreEqual(IncomingDtTraceId, payload.TraceId);
		}

		[Test]
		public void PayloadShouldHaveExpectedTraceIdValueWhenNoTraceIdReceived()
		{
			// Arrange
			var transaction = BuildMockTransaction();
			Mock.Arrange(() => transaction.TraceId).Returns((string) null);

			var segment = Mock.Create<ISegment>();
			var expectedTraceIdValue = transaction.Guid;

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.AreEqual(expectedTraceIdValue, payload.TraceId);
		}

		[Test]
		public void TraceIdShouldBeSameForAllSpansWhenNoTraceIdReceived()
		{
			// Arrange
			var transaction = BuildMockTransaction();

			var segment1 = Mock.Create<ISegment>();
			var segment2 = Mock.Create<ISegment>();

			// Act
			var firstHeaders = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment1);

			var firstEncodedJson = firstHeaders.HttpSafe();
			var payload1 = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(firstEncodedJson);

			var secondHeaders = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment2);
			var secondEncodedJson = secondHeaders.HttpSafe();
			var payload2 = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(secondEncodedJson);

			// Assert
			Assert.AreEqual(payload1.TraceId, payload2.TraceId);
		}

		#endregion TraceID Tests

		#endregion

		#region helpers

		private static IInternalTransaction BuildMockTransaction(bool hasIncomingPayload = false, bool? sampled = false)
		{
			var transaction = Mock.Create<IInternalTransaction>();
			var transactionMetadata = Mock.Create<ITransactionMetadata>();
			Mock.Arrange(() => transaction.TransactionMetadata).Returns(transactionMetadata);
			
			Mock.Arrange(() => transaction.Guid).Returns(GuidGenerator.GenerateNewRelicGuid());

			Mock.Arrange(() => transaction.Priority).Returns(Priority);
			Mock.Arrange(() => transaction.Sampled).Returns(sampled);
			Mock.Arrange(() => transaction.TraceId).Returns(GuidGenerator.GenerateNewRelicTraceId());

			if (hasIncomingPayload)
			{
				Mock.Arrange(() => transaction.TracingState).Returns(BuildMockTracingState());
				Mock.Arrange(() => transaction.Priority).Returns(IncomingPriority);
				Mock.Arrange(() => transaction.TraceId).Returns(IncomingDtTraceId);
			}
			else
			{
				ITracingState nullTracingState = null;
				Mock.Arrange(() => transaction.TracingState).Returns(nullTracingState);
			}

			return transaction;
		}

		private static DistributedTracePayload BuildSampleDistributedTracePayload()
		{
			return DistributedTracePayload.TryBuildOutgoingPayload(
				IncomingDtType.ToString(),
				IncomingAccountId,
				AgentApplicationId,
				IncomingDtGuid,
				IncomingDtTraceId,
				IncomingTrustKey,
				IncomingPriority,
				false,
				DateTime.UtcNow,
				null);				
		}

		private static ITracingState BuildMockTracingState()
		{
			var tracingState = Mock.Create<ITracingState>();

			Mock.Arrange(() => tracingState.Type).Returns(IncomingDtType);
			Mock.Arrange(() => tracingState.AppId).Returns(IncomingApplicationId);
			Mock.Arrange(() => tracingState.AccountId).Returns(IncomingAccountId);
			Mock.Arrange(() => tracingState.Guid).Returns(IncomingDtGuid);

			return tracingState;
		}

		#endregion helpers
	}
}
