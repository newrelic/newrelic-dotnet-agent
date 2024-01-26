// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NUnit.Framework;
using System;
using Telerik.JustMock;
using NewRelic.Core.DistributedTracing;
using NewRelic.Core;

namespace NewRelic.Agent.Core.DistributedTracing
{
    [TestFixture]
    public class DistributedTracePayloadTests
    {
        private const string TransportType = "HTTP";
        private const string AccountId = "56789";
        private const string AppId = "12345";
        private const string Guid = "12345";
        private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
        private const string TrustKey = "12345";
        private const float Priority = .5f;
        private const bool Sampled = true;
        private static DateTime Timestamp = DateTime.UtcNow;
        private const string TransactionId = "12345";

        private const DistributedTracingParentType Type = DistributedTracingParentType.App;

        private IConfiguration _configuration;

        [SetUp]
        public void Setup()
        {
            _configuration = Mock.Create<IConfiguration>();

            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.AccountId).Returns(AccountId);
            Mock.Arrange(() => _configuration.PrimaryApplicationId).Returns(AppId);
            Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(TrustKey);

            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);
        }

        [TestCase(null, AccountId, AppId, TraceId)]
        [TestCase(TransportType, null, AppId, TraceId)]
        [TestCase(TransportType, AccountId, null, TraceId)]
        [TestCase(TransportType, AccountId, AppId, null)]
        [TestCase("", AccountId, AppId, TraceId)]
        [TestCase(TransportType, "", AppId, TraceId)]
        [TestCase(TransportType, AccountId, "", TraceId)]
        [TestCase(TransportType, AccountId, AppId, "")]
        public void BuildOutgoingPayload_ReturnsNull_WhenRequiredFieldsNotPresent(string type, string accountId, string appId, string traceId)
        {
            var payload = DistributedTracePayload.TryBuildOutgoingPayload(type, accountId, appId, Guid, traceId, TrustKey, Priority, Sampled, Timestamp, TransactionId);
            Assert.That(payload, Is.Null);
        }

        [TestCase(null, null)]
        [TestCase("", "")]
        public void TryBuildOutgoingPayload_ReturnsNull_WhenDoesNotContainGuidOrTransactionId(string guid, string transactionId)
        {
            var payload = DistributedTracePayload.TryBuildOutgoingPayload(TransportType, AccountId, AppId, guid, TraceId,
                TrustKey, Priority, Sampled, Timestamp, transactionId);
            Assert.That(payload, Is.Null);
        }

        [Test]
        public void SerializeAndEncodeDistributedTracePayload_CreatesCorrectEncodedString()
        {
            var payload = BuildSampleDistributedTracePayload();
            var jsonString = payload.ToJson();
            var encodedJsonString = Strings.Base64Encode(jsonString);
            var serializedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

            Assert.That(serializedPayload, Is.EqualTo(encodedJsonString));
        }

        [Test]
        public void TryDecodeAndDeserializeDistributedTracePayload_ThrowsException_IfInvalidVersion()
        {
            var payload = BuildSampleDistributedTracePayload();
            payload.Version = new int[] { 9999, 1 };
            var encodedString = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(payload);

            Assert.Throws<DistributedTraceAcceptPayloadVersionException>(() => DistributedTracePayload.TryDecodeAndDeserializeDistributedTracePayload(encodedString));
        }


        [Test]
        public void TryDecodeAndDeserializeDistributedTracePayload_ThrowsException_IfEncodedAsInvalidType()
        {
            // The following base64 string isn't an encoding of a DistributedTracePayload but it is valid base64.
            var encodedString = "eyJ2IjpbMCwxXSwiZCI6eyJEaWZmZXJlbnQiOiJUeXBlIiwidHkiOiJBcHAiLCJhYyI6IjkxMjMiLCJhcCI6IjUxNDI0IiwiaWQiOiI1ZjQ3NGQ2NGI5Y2M5YjJhIiwidHIiOiIzMjIxYmYwOWFhMGJjZjBkIiwidGsiOiIxMjM0NSIsInByIjowLjEyMzQsInNhIjpmYWxzZSwidGkiOjE1Mjk0MjQxMzA2MDMsInR4IjoiMjc4NTZmNzBkM2QzMTRiNyJ9fQ==";
            Assert.Throws<DistributedTraceAcceptPayloadParseException>(() => DistributedTracePayload.TryDecodeAndDeserializeDistributedTracePayload(encodedString));
        }

        #region helpers

        private static DistributedTracePayload BuildSampleDistributedTracePayload()
        {
            return DistributedTracePayload.TryBuildOutgoingPayload(
                Type.ToString(),
                AccountId,
                AppId,
                Guid,
                TraceId,
                TrustKey,
                Priority,
                Sampled,
                Timestamp,
                TransactionId);
        }

        #endregion helpers
    }
}
