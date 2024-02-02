// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing
{
    [TestFixture]
    public class DistributedTracePayloadHandlerTests
    {
        private const string NewRelicPayloadHeaderName = "newrelic";
        private const string TracestateHeaderName = "tracestate";
        private const string TraceParentHeaderName = "traceparent";
        private readonly DateTime mockStartTime = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(3));

        private const DistributedTracingParentType DtTypeApp = DistributedTracingParentType.App;
        private const DistributedTracingParentType IncomingDtType = DistributedTracingParentType.Mobile;
        private const string AgentAccountId = "111111";
        private const string IncomingAccountId = "222222";
        private const string AgentApplicationId = "238575";
        private const string IncomingApplicationId = "888888";
        private const string IncomingDtGuid = "6d10a3dfbc4448a1";
        private const string IncomingDtTraceId = "e6463956f2c14ddaa3f737104381714f";
        private const string IncomingTrustKey = "12345";
        private const float Priority = 0.5f;
        private const float IncomingPriority = 0.75f;
        private const string TransactionId = "b85cf40e58084c21";

        private static DateTime Timestamp = DateTime.UtcNow;
        private const string ParentId = "b7ad6b7169203331";


        private const string ValidTraceparent = "00-" + IncomingDtTraceId + "-" + ParentId + "-01";
        private const string InvalidTraceparent = "00-12345-56789-01";

        private static readonly string ValidTracestate = IncomingTrustKey + "@nr=0-" + (int)DtTypeApp + "-" + AgentAccountId + "-" + AgentApplicationId + "-" + IncomingDtGuid + "-" + TransactionId + "-1-" + Priority + "-" + Timestamp.ToUnixTimeMilliseconds() + ",dd=YzRiMTIxODk1NmVmZTE4ZQ,44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020";
        private static readonly string TracestateInvalidNrEntry = IncomingTrustKey + "@nr=-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222";
        private static readonly string TracestateNoNrEntry = (IncomingTrustKey + 1) + "@nr=-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222";

        // v:[2,5]
        private const string NewRelicPayloadWithUnsupportedVersion = "{ \"v\":[2,5],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\",\"tx\":\"transactionId\",\"id\":\"guid\"}}";
        // missing tx: AND id:
        private const string NewRelicPayloadUntraceable = "{ \"v\":[0,1],\"d\":{\"ty\":\"HTTP\",\"ac\":\"accountId\",\"ap\":\"appId\",\"tr\":\"traceId\",\"pr\":0.65,\"sa\":true,\"ti\":0,\"tk\":\"trustKey\"}}";


        private DistributedTracePayloadHandler _distributedTracePayloadHandler;
        private IConfiguration _configuration;
        private IAdaptiveSampler _adaptiveSampler;
        private IAgentHealthReporter _agentHealthReporter;

        private readonly TransactionName _initialTransactionName = TransactionName.ForWebTransaction("initialCategory", "initialName");
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

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
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
        }

       #region Accept Incoming Request

        [Test]
        public void TryDecodeInboundSerializedDistributedTracePayload_ReturnsValidPayload()
        {
            // Arrange
            var payload = BuildSampleDistributedTracePayload();
            payload.TransactionId = TransactionId;
            payload.Guid = null;

            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

            // Act
            var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            // Assert
            Assert.That(decodedPayload, Is.Not.Null);

            NrAssert.Multiple(
                () => Assert.That(decodedPayload.Type, Is.EqualTo(IncomingDtType.ToString())),
                () => Assert.That(decodedPayload.AccountId, Is.EqualTo(IncomingAccountId)),
                () => Assert.That(decodedPayload.AppId, Is.EqualTo(AgentApplicationId)),
                () => Assert.That(decodedPayload.Guid, Is.EqualTo(null)),
                () => Assert.That(decodedPayload.TrustKey, Is.EqualTo(IncomingTrustKey)),
                () => Assert.That(decodedPayload.Priority, Is.EqualTo(IncomingPriority)),
                () => Assert.That(decodedPayload.Sampled, Is.EqualTo(false)),
                () => Assert.That(decodedPayload.Timestamp, Is.LessThan(DateTime.UtcNow)),
                () => Assert.That(decodedPayload.TransactionId, Is.EqualTo(TransactionId))
            );
        }

        [Test]
        public void PayloadShouldBeNullWhenTrustKeyNotTrusted()
        {
            // Arrange
            Mock.Arrange(() => _configuration.TrustedAccountKey).Returns("NOPE");

            var payload = BuildSampleDistributedTracePayload();
            payload.TransactionId = TransactionId;

            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

            // Act
            var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            // Assert
            Assert.That(decodedPayload, Is.Null);
        }

        [Test]
        public void PayloadShouldBePopulatedWhenTrustKeyTrusted()
        {
            // Arrange
            var payload = BuildSampleDistributedTracePayload();
            payload.TransactionId = TransactionId;

            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

            // Act
            var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            // Assert
            Assert.That(decodedPayload, Is.Not.Null);
        }

        [Test]
        public void PayloadShouldBeNullWhenTrustKeyNullAndAccountIdNotTrusted()
        {
            // Arrange
            Mock.Arrange(() => _configuration.TrustedAccountKey).Returns("NOPE");

            var payload = BuildSampleDistributedTracePayload();
            payload.TrustKey = null;
            payload.TransactionId = TransactionId;

            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

            // Act
            var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            // Assert
            Assert.That(decodedPayload, Is.Null);
        }

        [Test]
        public void PayloadShouldBePopulatedWhenTrustKeyNullAndAccountIdTrusted()
        {
            // Arrange
            Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(IncomingAccountId);

            var payload = BuildSampleDistributedTracePayload();
            payload.TrustKey = null;
            payload.TransactionId = TransactionId;

            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

            // Act
            var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            // Assert
            Assert.That(decodedPayload, Is.Not.Null);
        }

        [Test]
        public void TryDecodeInboundSerializedDistributedTracePayload_ReturnsNull_IfHigherMajorVersion()
        {
            // Arrange
            var payload = BuildSampleDistributedTracePayload();
            payload.Version = new[] { int.MaxValue, 1 };
            payload.TransactionId = TransactionId;

            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

            // Act
            var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            // Assert
            Assert.That(decodedPayload, Is.Null);
        }

        [Test]
        public void ShouldNotCreatePayloadWhenGuidAndTransactionIdNull()
        {
            // Arrange
            var payload = BuildSampleDistributedTracePayload();
            payload.Guid = null;

            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

            // Act
            var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            // Assert
            Assert.That(decodedPayload, Is.Null);
        }

        [Test]
        public void ShouldGenerateParseExceptionMetricWhenGuidAndTransactionIdNull()
        {
            // Arrange
            var payload = BuildSampleDistributedTracePayload();
            payload.Guid = null;
            payload.TransactionId = null;

            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

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

            // Act
            var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
            var encodedJson = model.HttpSafe();

            DistributedTracePayload dtPayload = null;
            Assert.That(() => dtPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson), Is.Not.Null);

            // Assert
            NrAssert.Multiple(
                () => Assert.That(dtPayload.Type, Is.EqualTo(DtTypeApp.ToString())),
                () => Assert.That(dtPayload.AccountId, Is.EqualTo(AgentAccountId)),
                () => Assert.That(dtPayload.AppId, Is.EqualTo(AgentApplicationId)),
                () => Assert.That(dtPayload.Guid, Is.EqualTo(null)),
                () => Assert.That(dtPayload.TraceId, Is.EqualTo(transaction.TraceId)),
                () => Assert.That(dtPayload.Priority, Is.EqualTo(Priority)),
                () => Assert.That(dtPayload.Timestamp, Is.LessThan(DateTime.UtcNow)),
                () => Assert.That(dtPayload.TransactionId, Is.EqualTo($"{transaction.Guid}"))
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
            Assert.That(() => dtPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson), Is.Not.Null);

            // Assert
            NrAssert.Multiple(
                () => Assert.That(dtPayload.Type, Is.EqualTo(DtTypeApp.ToString())),
                () => Assert.That(dtPayload.AccountId, Is.EqualTo(AgentAccountId)),
                () => Assert.That(dtPayload.AppId, Is.EqualTo(AgentApplicationId)),
                () => Assert.That(dtPayload.Guid, Is.EqualTo(null)),
                () => Assert.That(dtPayload.TraceId, Is.EqualTo(IncomingDtTraceId)),
                () => Assert.That(dtPayload.TrustKey, Is.EqualTo(IncomingTrustKey)),
                () => Assert.That(dtPayload.Priority, Is.EqualTo(IncomingPriority)),
                () => Assert.That(dtPayload.Timestamp, Is.LessThan(DateTime.UtcNow)),
                () => Assert.That(dtPayload.TransactionId, Is.EqualTo($"{transaction.Guid}"))
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
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);
            // Assert
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.TrustKey, Is.EqualTo(IncomingTrustKey));
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
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.TrustKey, Is.Null);
        }

        [Test]
        public void PayloadShouldHaveGuidWhenSpansEnabledAndTransactionSampled()
        {
            // Arrange
            Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

            var transaction = BuildMockTransaction(sampled: true);

            const string expectedGuid = "expectedId";
            var segment = Mock.Create<ISegment>();
            Mock.Arrange(() => segment.SpanId).Returns(expectedGuid);

            // Act
            var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
            var encodedJson = model.HttpSafe();
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload.Guid, Is.Not.Null);
            Assert.That(payload.Guid, Is.EqualTo(expectedGuid));
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
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload.Guid, Is.Null);
        }

        [Test]
        public void PayloadShouldHaveGuidWhenSpansEnabledAndTransactionNotSampled()
        {
            // Arrange
            Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

            var transaction = BuildMockTransaction(sampled: false);

            const string expectedGuid = "expectedId";
            var segment = Mock.Create<ISegment>();
            Mock.Arrange(() => segment.SpanId).Returns(expectedGuid);

            // Act
            var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
            var encodedJson = model.HttpSafe();
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload.Guid, Is.EqualTo(expectedGuid));
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
            Assert.That(payload, Is.EqualTo(DistributedTraceApiModel.EmptyModel));
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
            Assert.That(payload, Is.EqualTo(DistributedTraceApiModel.EmptyModel));
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
            Assert.That(payload, Is.EqualTo(DistributedTraceApiModel.EmptyModel));
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
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload.TransactionId, Is.Null);
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
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload.TransactionId, Is.Not.Null);
            Assert.That(payload.TransactionId, Is.EqualTo(transaction.Guid));
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
            Assert.That(payload, Is.EqualTo(DistributedTraceApiModel.EmptyModel));
        }

        [Test]
        public void TryDecodeInboundSerializedDistributedTracePayload_ReturnsCorrectDeserializedObject()
        {
            var payload = BuildSampleDistributedTracePayload();
            var encodedString = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);
            var deserializedObject = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedString);

            NrAssert.Multiple(
                () => Assert.That(deserializedObject, Is.Not.Null),
                () => Assert.That(deserializedObject.GetType(), Is.EqualTo(typeof(DistributedTracePayload))),
                () => Assert.That(deserializedObject.AccountId, Is.EqualTo(payload.AccountId)),
                () => Assert.That(deserializedObject.AppId, Is.EqualTo(payload.AppId)),
                () => Assert.That(deserializedObject.Guid, Is.EqualTo(payload.Guid)),
                () => Assert.That(deserializedObject.Priority, Is.EqualTo(payload.Priority)),
                () => Assert.That(deserializedObject.Sampled, Is.EqualTo(payload.Sampled)),
                () => Assert.That(deserializedObject.TraceId, Is.EqualTo(payload.TraceId)),
                () => Assert.That(deserializedObject.TrustKey, Is.EqualTo(payload.TrustKey)),
                () => Assert.That(deserializedObject.Type, Is.EqualTo(payload.Type)),
                () => Assert.That(deserializedObject.Version, Is.EqualTo(payload.Version)),
                () => Assert.That(deserializedObject.TransactionId, Is.EqualTo(payload.TransactionId))
            );
        }

        [Test]
        public void TryDecodeInboundSerializedDistributedTracePayload_UnencodedObject_ReturnsCorrectDeserializedObject()
        {
            var payload = BuildSampleDistributedTracePayload();
            var deserializedObject = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(payload.ToJson());

            NrAssert.Multiple(
                () => Assert.That(deserializedObject, Is.Not.Null),
                () => Assert.That(deserializedObject.GetType(), Is.EqualTo(typeof(DistributedTracePayload))),

                () => Assert.That(deserializedObject.AccountId, Is.EqualTo(payload.AccountId)),
                () => Assert.That(deserializedObject.AppId, Is.EqualTo(payload.AppId)),
                () => Assert.That(deserializedObject.Guid, Is.EqualTo(payload.Guid)),
                () => Assert.That(deserializedObject.Priority, Is.EqualTo(payload.Priority)),
                () => Assert.That(deserializedObject.Sampled, Is.EqualTo(payload.Sampled)),
                () => Assert.That(deserializedObject.Timestamp.ToUnixTimeMilliseconds(), Is.EqualTo(payload.Timestamp.ToUnixTimeMilliseconds())),
                () => Assert.That(deserializedObject.TraceId, Is.EqualTo(payload.TraceId)),
                () => Assert.That(deserializedObject.TrustKey, Is.EqualTo(payload.TrustKey)),
                () => Assert.That(deserializedObject.Type, Is.EqualTo(payload.Type)),
                () => Assert.That(deserializedObject.Version, Is.EqualTo(payload.Version)),
                () => Assert.That(deserializedObject.TransactionId, Is.EqualTo(payload.TransactionId))
            );
        }

        [Test]
        public void TryDecodeInboundSerializedDistributedTracePayload_ReturnsNull_IfInvalidBase64()
        {
            var payload = BuildSampleDistributedTracePayload();
            var encodedString = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);
            var badEncodedString = "badbasd64string" + encodedString;
            var deserializedObject = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(badEncodedString);

            Assert.That(deserializedObject, Is.Null);
        }

        [Test]
        public void BuildIncomingPayloadFromJson_ReturnsNull_WhenNeitherGuidOrTransactionIdSet()
        {
            var encodedPayload = "eyJ2IjpbMCwxXSwiZCI6eyJ0eSI6IkFwcCIsImFjIjoiOTEyMyIsImFwIjoiNTE0MjQiLCJ0ciI6IjMyMjFiZjA5YWEwYmNmMGQiLCJwciI6MC4xMjM0LCJzYSI6ZmFsc2UsInRpIjoxNDgyOTU5NTI1NTc3fX0=";
            var distributedTracePayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException(), Occurs.Once());
            Assert.That(distributedTracePayload, Is.Null);
        }

        [Test]
        public void BuildIncomingPayloadFromJson_ReturnsNotNull_WithSuccessMetricsEnabled()
        {
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var encodedPayload = "eyJ2IjpbMCwxXSwiZCI6eyJhYyI6IjEyMzQ1IiwiYXAiOiIyODI3OTAyIiwiaWQiOiI3ZDNlZmIxYjE3M2ZlY2ZhIiwidHgiOiJlOGI5MWExNTkyODlmZjc0IiwicHIiOjEuMjM0NTY3LCJzYSI6dHJ1ZSwidGkiOjE1MTg0Njk2MzYwMzUsInRyIjoiZDZiNGJhMGMzYTcxMmNhIiwidHkiOiJBcHAifX0=";

            var distributedTracePayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadSuccess(), Occurs.Once());
            Assert.That(distributedTracePayload, Is.Not.Null);
        }

        [Test]
        public void BuildIncomingPayloadFromJson_ReturnsNotNull_WithSuccessMetricsDisabled()
        {
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(false);

            var encodedPayload = "eyJ2IjpbMCwxXSwiZCI6eyJhYyI6IjEyMzQ1IiwiYXAiOiIyODI3OTAyIiwiaWQiOiI3ZDNlZmIxYjE3M2ZlY2ZhIiwidHgiOiJlOGI5MWExNTkyODlmZjc0IiwicHIiOjEuMjM0NTY3LCJzYSI6dHJ1ZSwidGkiOjE1MTg0Njk2MzYwMzUsInRyIjoiZDZiNGJhMGMzYTcxMmNhIiwidHkiOiJBcHAifX0=";
            var distributedTracePayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadSuccess(), Occurs.Never());
            Assert.That(distributedTracePayload, Is.Not.Null);
        }

        [Test]
        public void BuildOutgoingPayloadFromTransaction_ReturnsNotNull_WithSuccessMetricsEnabled()
        {
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var transaction = new Transaction(_configuration, _initialTransactionName, Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), 1.0f, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), _attribDefs);
            var headers = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceCreatePayloadSuccess(), Occurs.Once());
            Assert.That(headers, Is.Not.Null);
        }

        [Test]
        public void BuildOutgoingPayloadFromTransaction_ReturnsNotNull_WithSuccessMetricsDisabled()
        {
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(false);

            var transaction = new Transaction(_configuration, _initialTransactionName, Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), 1.0f, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), _attribDefs);
            var headers = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceCreatePayloadSuccess(), Occurs.Never());
            Assert.That(headers, Is.Not.Null);
        }

        #region W3C Tests
        [TestCase("getHeaders is null")]
        [TestCase("getHeaders throws exception")]
        public void W3C_AcceptDistributedTraceHeaders_DoesNotThrowException(string testCaseName)
        {
            // Arrange
            var transaction = Mock.Create<ITransaction>();
            var headers = new List<KeyValuePair<string, string>>();

            Func<IEnumerable<KeyValuePair<string, string>>, string, IList<string>> getHeaders = null;
            if (testCaseName != "getHeaders is null")
            {
                getHeaders = new Func<IEnumerable<KeyValuePair<string, string>>, string, IList<string>>((carrier, key) =>
               {
                   throw new Exception("Exception occurred in getHeaders.");
               });
            }

            var tracingState = Mock.Create<ITracingState>();
            var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

            Mock.Arrange(() => transaction.AcceptDistributedTraceHeaders(Arg.IsAny<IEnumerable<KeyValuePair<string, string>>>(), Arg.IsAny<Func<IEnumerable<KeyValuePair<string, string>>, string, IList<string>>>(), Arg.IsAny<TransportType>())).DoInstead(() => _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(headers, getHeaders, TransportType.HTTP, mockStartTime));

            // Act
            Assert.DoesNotThrow(() => transaction.AcceptDistributedTraceHeaders(headers, getHeaders, TransportType.HTTP));
            Assert.That(headers, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void W3C_InsertDistributedTraceHeaders_OutboundHeaders(bool hasIncomingPayload)
        {
            // Arrange
            Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var transaction = BuildMockTransaction(hasIncomingPayload: hasIncomingPayload, sampled: true);

            var transactionGuid = GuidGenerator.GenerateNewRelicGuid();
            Mock.Arrange(() => transaction.Guid).Returns(transactionGuid);

            var expectedSpanGuid = GuidGenerator.GenerateNewRelicGuid();
            var segment = Mock.Create<ISegment>();
            Mock.Arrange(() => segment.SpanId).Returns(expectedSpanGuid);

            Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

            var headers = new List<KeyValuePair<string, string>>();
            var setHeaders = new Action<List<KeyValuePair<string, string>>, string, string>((carrier, key, value) =>
            {
                carrier.Add(new KeyValuePair<string, string>(key, value));
            });

            var tracingState = Mock.Create<ITracingState>();

            var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

            Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
            Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

            Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<List<KeyValuePair<string, string>>>(), Arg.IsAny<Action<List<KeyValuePair<string, string>>, string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, headers, setHeaders));

            // Act
            transaction.InsertDistributedTraceHeaders(headers, setHeaders);

            Assert.Multiple(() =>
            {
                Assert.That(headers.Count(header => header.Key == NewRelicPayloadHeaderName), Is.GreaterThan(0), "There must be at least a newrelic header");
                Assert.That(headers.Count(header => header.Key == TraceParentHeaderName), Is.GreaterThan(0), "There must be at least a traceparent header");
                Assert.That(headers.Count(header => header.Key == TracestateHeaderName), Is.GreaterThan(0), "There must be at least a tracestate header");
            });

            var tracestateHeaderValue = headers.Where(header => header.Key == TracestateHeaderName).Select(header => header.Value).ToList();
            var traceState = W3CTracestate.GetW3CTracestateFromHeaders(tracestateHeaderValue, IncomingTrustKey);
            Assert.That(traceState, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(traceState.AccountId, Is.EqualTo(AgentAccountId));
                Assert.That(traceState.AccountKey, Is.EqualTo(IncomingTrustKey));
                Assert.That(traceState.AppId, Is.EqualTo(AgentApplicationId));
                Assert.That(traceState.TransactionId, Is.EqualTo(transactionGuid));
                Assert.That(traceState.ParentType, Is.EqualTo(DistributedTracingParentType.App));
                Assert.That(traceState.Priority, Is.EqualTo((hasIncomingPayload ? IncomingPriority : Priority)));
                Assert.That(traceState.SpanId, Is.EqualTo(expectedSpanGuid));
                Assert.That(traceState.VendorstateEntries.SequenceEqual(vendorStateEntries));
                Assert.That(traceState.Version, Is.EqualTo(0));
            });

            var traceParentHeaderValue = headers.Where(header => header.Key == TraceParentHeaderName).Select(header => header.Value).FirstOrDefault();

            var traceIdExpectedLength = 32;
            var traceparent = W3CTraceparent.GetW3CTraceParentFromHeader(traceParentHeaderValue);

            var expectedTraceId = transaction.TraceId;
            expectedTraceId = expectedTraceId.PadLeft(traceIdExpectedLength, '0').ToLowerInvariant();
            var expectedParentId = expectedSpanGuid;

            Assert.Multiple(() =>
            {
                Assert.That(traceparent.TraceId, Has.Length.EqualTo(traceIdExpectedLength));
                Assert.That(traceparent.TraceId, Is.EqualTo(expectedTraceId));
                Assert.That(traceparent.Version, Is.EqualTo(0));
                Assert.That(traceparent.ParentId, Is.EqualTo(expectedParentId));
                Assert.That(traceparent.TraceFlags, Is.EqualTo("01"));
            });

            var nrHeaderValue = headers.Where(header => header.Key == NewRelicPayloadHeaderName).Select(header => header.Value).FirstOrDefault();
            Assert.That(nrHeaderValue, Is.Not.Null);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityTraceContextCreateSuccess(), Occurs.Once());
        }

        [Test]
        public void W3C_InsertDistributedTraceHeaders_ExcludeNewRelicHeader()
        {
            // Arrange
            Mock.Arrange(() => _configuration.ExcludeNewrelicHeader).Returns(true);
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var transaction = BuildMockTransaction(hasIncomingPayload: true, sampled: true);

            Mock.Arrange(() => transaction.Guid).Returns(GuidGenerator.GenerateNewRelicGuid());

            var segment = Mock.Create<ISegment>();
            Mock.Arrange(() => segment.SpanId).Returns(GuidGenerator.GenerateNewRelicGuid());
            Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

            var headers = new List<KeyValuePair<string, string>>();
            var setHeaders = new Action<List<KeyValuePair<string, string>>, string, string>((carrier, key, value) =>
            {
                carrier.Add(new KeyValuePair<string, string>(key, value));
            });

            var tracingState = Mock.Create<ITracingState>();
            var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

            Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
            Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

            Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<List<KeyValuePair<string, string>>>(), Arg.IsAny<Action<List<KeyValuePair<string, string>>, string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, headers, setHeaders));

            // Act
            transaction.InsertDistributedTraceHeaders(headers, setHeaders);

            Assert.Multiple(() =>
            {
                Assert.That(headers, Has.Count.EqualTo(2));
                Assert.That(headers.Count(header => header.Key == NewRelicPayloadHeaderName), Is.EqualTo(0), "There should not be a newrelic header");
                Assert.That(headers.Count(header => header.Key == TraceParentHeaderName), Is.GreaterThan(0), "There must be at least a traceparent header");
                Assert.That(headers.Count(header => header.Key == TracestateHeaderName), Is.GreaterThan(0), "There must be at least a tracestate header");
            });
            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityTraceContextCreateSuccess(), Occurs.Once());
        }

        //This test makes sure string presentation for Priority in a tracestate is culture independent.
        [TestCase(1.123000f, "1.123")]
        public void W3C_InsertDistributedTraceHeaders_CultureIndependent_PriorityInRightFormat(float testPriority, string expectedPriorityString)
        {
            //Set up "eu-ES" culture for this test thread. In this culture, calling ToString() on a float of 1.123f will result this string "1,123" instead of "1.123". 
            var ci = new CultureInfo("eu-ES");
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;

            // Arrange
            Mock.Arrange(() => _configuration.ExcludeNewrelicHeader).Returns(true);
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var transaction = BuildMockTransaction(sampled: true);
            Mock.Arrange(() => transaction.Priority).Returns(testPriority);

            Mock.Arrange(() => transaction.Guid).Returns(GuidGenerator.GenerateNewRelicGuid());

            var segment = Mock.Create<ISegment>();
            Mock.Arrange(() => segment.SpanId).Returns(GuidGenerator.GenerateNewRelicGuid());
            Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

            var headers = new List<KeyValuePair<string, string>>();
            var setHeaders = new Action<List<KeyValuePair<string, string>>, string, string>((carrier, key, value) =>
            {
                carrier.Add(new KeyValuePair<string, string>(key, value));
            });

            var tracingState = Mock.Create<ITracingState>();
            var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

            Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
            Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

            Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<List<KeyValuePair<string, string>>>(), Arg.IsAny<Action<List<KeyValuePair<string, string>>, string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, headers, setHeaders));

            // Act
            transaction.InsertDistributedTraceHeaders(headers, setHeaders);

            var tracestateHeaderValue = headers.Where(header => header.Key == TracestateHeaderName).Select(header => header.Value).ToList();
            var priorityIndex = 7;
            var priorityString = tracestateHeaderValue[0].Split('-')[priorityIndex];
            Assert.That(priorityString, Is.EqualTo(expectedPriorityString));

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityTraceContextCreateSuccess(), Occurs.Once());
        }

        [TestCase(.1234567f, "0.123457")]
        [TestCase(1.123000f, "1.123")]
        public void W3C_InsertDistributedTraceHeaders_PriorityInRightFormat(float testPriority, string expectedPriorityString)
        {
            // Arrange
            Mock.Arrange(() => _configuration.ExcludeNewrelicHeader).Returns(true);
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var transaction = BuildMockTransaction(sampled: true);
            Mock.Arrange(() => transaction.Priority).Returns(testPriority);

            Mock.Arrange(() => transaction.Guid).Returns(GuidGenerator.GenerateNewRelicGuid());

            var segment = Mock.Create<ISegment>();
            Mock.Arrange(() => segment.SpanId).Returns(GuidGenerator.GenerateNewRelicGuid());
            Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

            var headers = new List<KeyValuePair<string, string>>();
            var setHeaders = new Action<List<KeyValuePair<string, string>>, string, string>((carrier, key, value) =>
             {
                 carrier.Add(new KeyValuePair<string, string>(key, value));
             });

            var tracingState = Mock.Create<ITracingState>();
            var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

            Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
            Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

            Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<List<KeyValuePair<string, string>>>(), Arg.IsAny<Action<List<KeyValuePair<string, string>>, string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, headers, setHeaders));

            // Act
            transaction.InsertDistributedTraceHeaders(headers, setHeaders);

            var tracestateHeaderValue = headers.Where(header => header.Key == TracestateHeaderName).Select(header => header.Value).ToList();
            var priorityIndex = 7;
            var priorityString = tracestateHeaderValue[0].Split('-')[priorityIndex];
            Assert.That(priorityString, Is.EqualTo(expectedPriorityString));

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityTraceContextCreateSuccess(), Occurs.Once());
        }

        [TestCase("E6463956F2C14DDAA3F737104381714F", "e6463956f2c14ddaa3f737104381714f")]
        [TestCase("E6463956F2C14DDA", "0000000000000000e6463956f2c14dda")]
        [TestCase("e6463956f2c14dda", "0000000000000000e6463956f2c14dda")]
        public void W3C_InsertDistributedTraceHeaders_TraceIdInRightFormat(string testTraceId, string expectedTraceId)
        {
            // Arrange
            Mock.Arrange(() => _configuration.ExcludeNewrelicHeader).Returns(true);
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var transaction = BuildMockTransaction(hasIncomingPayload: true, sampled: true);

            Mock.Arrange(() => transaction.TraceId).Returns(testTraceId);

            var segment = Mock.Create<ISegment>();
            Mock.Arrange(() => segment.SpanId).Returns(GuidGenerator.GenerateNewRelicGuid());
            Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

            var headers = new List<KeyValuePair<string, string>>();
            var setHeaders = new Action<List<KeyValuePair<string, string>>, string, string>((carrier, key, value) =>
            {
                carrier.Add(new KeyValuePair<string, string>(key, value));
            });

            var tracingState = Mock.Create<ITracingState>();
            var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

            Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
            Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

            Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<List<KeyValuePair<string, string>>>(), Arg.IsAny<Action<List<KeyValuePair<string, string>>, string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, headers, setHeaders));

            // Act
            transaction.InsertDistributedTraceHeaders(headers, setHeaders);

            var traceParentHeaderValue = headers.Where(header => header.Key == TraceParentHeaderName).Select(header => header.Value).ToList();
            var traceIdIndex = 1;
            var traceId = traceParentHeaderValue[0].Split('-')[traceIdIndex];
            Assert.That(traceId, Is.EqualTo(expectedTraceId));
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

            Action<List<KeyValuePair<string, string>>, string, string> setHeaders = null;
            if (testCaseName != "setHeaders is null")
            {
                setHeaders = new Action<List<KeyValuePair<string, string>>, string, string>((carrier, key, value) =>
                {
                    throw new Exception("Exception occurred in setHeaders.");
                });
            }

            var tracingState = Mock.Create<ITracingState>();
            var vendorStateEntries = new List<string> { "k1=v1", "k2=v2" };

            Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
            Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

            Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(Arg.IsAny<List<KeyValuePair<string, string>>>(), Arg.IsAny<Action<List<KeyValuePair<string, string>>, string, string>>())).DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, headers, setHeaders));

            // Act
            Assert.DoesNotThrow(() => transaction.InsertDistributedTraceHeaders(headers, setHeaders));
            Assert.That(headers, Is.Empty);
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
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload.TraceId, Is.Not.Null);
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
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload.TraceId, Is.Not.Null);
        }

        [Test]
        public void TraceIdShouldBeSameAsIncomingTraceIdWhenReceived()
        {
            var transaction = BuildMockTransaction(hasIncomingPayload: true);
            var segment = Mock.Create<ISegment>();

            // Act
            var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
            var encodedJson = model.HttpSafe();

            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload.TraceId, Is.EqualTo(IncomingDtTraceId));
        }

        [Test]
        public void PayloadShouldHaveExpectedTraceIdValueWhenNoTraceIdReceived()
        {
            // Arrange
            var transaction = BuildMockTransaction();

            var txTraceId = GuidGenerator.GenerateNewRelicTraceId();
            Mock.Arrange(() => transaction.TraceId).Returns(txTraceId);

            var segment = Mock.Create<ISegment>();

            // Act
            var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
            var encodedJson = model.HttpSafe();
            var payload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedJson);

            // Assert
            Assert.That(payload.TraceId, Is.EqualTo(txTraceId));
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
            var payload1 = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(firstEncodedJson);

            var secondHeaders = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment2);
            var secondEncodedJson = secondHeaders.HttpSafe();
            var payload2 = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(secondEncodedJson);

            // Assert
            Assert.That(payload2.TraceId, Is.EqualTo(payload1.TraceId));
        }

        #endregion TraceID Tests

        [TestCase(true)]
        [TestCase(true, "")]
        [TestCase(true, "k1=v1", "k2=v2")]
        [TestCase(false)]
        [TestCase(false, "")]
        [TestCase(false, "k1=v1", "k2=v2")]
        public void W3C_BuildTracestate_EmptyVendors_NoCommas(bool hasIncomingPayload, params string[] vendorState)
        {
            // Arrange
            Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var transaction = BuildMockTransaction(hasIncomingPayload: hasIncomingPayload, sampled: true);

            var transactionGuid = GuidGenerator.GenerateNewRelicGuid();
            Mock.Arrange(() => transaction.Guid).Returns(transactionGuid);

            var expectedSpanGuid = GuidGenerator.GenerateNewRelicGuid();
            var segment = Mock.Create<ISegment>();
            Mock.Arrange(() => segment.SpanId).Returns(expectedSpanGuid);

            Mock.Arrange(() => transaction.CurrentSegment).Returns(segment);

            var headers = new List<KeyValuePair<string, string>>();
            var setHeaders = new Action<List<KeyValuePair<string, string>>, string, string>((carrier, key, value) =>
            {
                carrier.Add(new KeyValuePair<string, string>(key, value));
            });

            var tracingState = Mock.Create<ITracingState>();

            var vendorStateEntries = vendorState.ToList();

            Mock.Arrange(() => tracingState.VendorStateEntries).Returns(vendorStateEntries);
            Mock.Arrange(() => transaction.TracingState).Returns(tracingState);

            Mock.Arrange(() => transaction.InsertDistributedTraceHeaders(
                Arg.IsAny<List<KeyValuePair<string, string>>>(),
                Arg.IsAny<Action<List<KeyValuePair<string, string>>, string, string>>()))
                    .DoInstead(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(transaction, headers, setHeaders));

            // Act
            transaction.InsertDistributedTraceHeaders(headers, setHeaders);

            var tracestateHeaderValue = headers.Where(header => header.Key == TracestateHeaderName).Select(header => header.Value).FirstOrDefault();

            Assert.That(tracestateHeaderValue, Does.Not.EndWith(","), "W3C Tracestate string has a trailing comma.");
        }

        #endregion

        #region Supportability Metrics
        [Test]
        public void AcceptDistributedTraceHeaders_NewRelicPayload_CorrectPayload_GeneratesAcceptSuccessSupportabilityMetric_WithSuccessMetricsEnabled()
        {
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(BuildSampleDistributedTracePayload());

            var headers = new Dictionary<string, string>() { { NewRelicPayloadHeaderName.ToLower(), encodedPayload } };

            var tracingState = _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(headers, GetHeaders, TransportType.HTTP, mockStartTime);


            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadSuccess(), Occurs.Once());
        }
        [Test]
        public void AcceptDistributedTraceHeaders_NewRelicPayload_UntraceablePayload_GeneratesParseExceptionSupportabilityMetric()
        {
            var headers = new Dictionary<string, string>() { { NewRelicPayloadHeaderName.ToLower(), Strings.Base64Encode(NewRelicPayloadUntraceable) } };
            var tracingState = _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(headers, GetHeaders, Extensions.Providers.Wrapper.TransportType.HTTP, mockStartTime);


            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException(), Occurs.Once());
        }

        [Test]
        public void AcceptDistributedTraceHeaders_NewRelicPayload_UnsupportedVersion_GeneratesPayloadIgnoredSupportabilityMetric()
        {
            var headers = new Dictionary<string, string>() { { NewRelicPayloadHeaderName.ToLower(), Strings.Base64Encode(NewRelicPayloadWithUnsupportedVersion) } };
            var tracingState = _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(headers, GetHeaders, Extensions.Providers.Wrapper.TransportType.HTTP, mockStartTime);


            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion(), Occurs.Once());
        }

        [Test]
        public void AcceptDistributedTraceHeaders_TraceContext_CorrectPayload_GeneratesAcceptSuccessSupportabilityMetric_WithSucessMetricsEnabled()
        {
            Mock.Arrange(() => _configuration.PayloadSuccessMetricsEnabled).Returns(true);

            var headers = new Dictionary<string, string>()
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", ValidTracestate },
            };

            var tracingState = _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(headers, GetHeaders, Extensions.Providers.Wrapper.TransportType.HTTP, mockStartTime);


            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityTraceContextAcceptSuccess(), Occurs.Once());
        }

        [Test]
        public void AcceptDistributedTraceHeaders_TraceContext_InvalidTraceParent_GeneratesTraceParentParseExceptionSupportabilityMetric()
        {
            var headers = new Dictionary<string, string>()
            {
                { "traceparent", InvalidTraceparent },
                { "tracestate", ValidTracestate },
            };

            var tracingState = _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(headers, GetHeaders, Extensions.Providers.Wrapper.TransportType.HTTP, mockStartTime);


            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityTraceContextTraceParentParseException(), Occurs.Once());
        }

        [Test]
        public void AcceptDistributedTraceHeaders_TraceContext_TracestateInvalidNrEntry_GeneratesTraceStateInvalidNrEntrySupportabilityMetric()
        {
            var headers = new Dictionary<string, string>()
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", TracestateInvalidNrEntry },
            };

            var tracingState = _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(headers, GetHeaders, Extensions.Providers.Wrapper.TransportType.HTTP, mockStartTime);


            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityTraceContextTraceStateInvalidNrEntry(), Occurs.Once());
        }

        [Test]
        public void AcceptDistributedTraceHeaders_TraceContext_TracestateNoNrEntry_GeneratesTraceStateNoNrEntrySupportabilityMetric()
        {
            var headers = new Dictionary<string, string>()
            {
                { "traceparent", ValidTraceparent },
                { "tracestate", TracestateNoNrEntry },
            };

            var tracingState = _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(headers, GetHeaders, TransportType.HTTP, mockStartTime);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityTraceContextTraceStateNoNrEntry(), Occurs.Once());
        }

        #endregion Supportability Metrics

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

        private static IList<string> GetHeaders(IEnumerable<KeyValuePair<string, string>> carrier, string key)
        {
            var headerValues = new List<string>();

            foreach (var item in carrier)
            {
                if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    headerValues.Add(item.Value);
                }
            }

            return headerValues;
        }

        #endregion helpers
    }
}
