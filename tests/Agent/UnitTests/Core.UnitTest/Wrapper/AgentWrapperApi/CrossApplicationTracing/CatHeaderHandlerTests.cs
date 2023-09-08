// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Core;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
    [TestFixture]
    public class CatHeaderHandlerTests
    {
        private const string NewRelicIdHttpHeader = "X-NewRelic-ID";
        private const string AppDataHttpHeader = "X-NewRelic-App-Data";
        private const string TransactionDataHttpHeader = "X-NewRelic-Transaction";
        private const float ExpectedResponseTimeInSeconds = 0.5f;

        private CatHeaderHandler _catHeaderHandler;

        private IConfiguration _configuration;

        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(true);
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);
            var catMetrics = Mock.Create<ICATSupportabilityMetricCounters>();
            _catHeaderHandler = new CatHeaderHandler(configurationService, catMetrics);
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        }

        #region inbound CAT request - outbound CAT response

        private List<string> GetHeaderValue(Dictionary<string, string> headers, string key)
        {
            var headerValues = new List<string>();
            foreach (var item in headers)
            {
                if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    headerValues.Add(item.Value);
                }
            }
            return headerValues;
        }

        [Test]
        public void TryDecodeInboundRequestHeaders_ReturnsNull_IfCatIsDisabled()
        {
            Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(false);
            var headers = new Dictionary<string, string>
            {
                {TransactionDataHttpHeader, Strings.Base64Encode("[\"crossProcessId\",\"transactionName\",1.1,2.2,3,null,false]")}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundRequestHeaders(headers, GetHeaderValue);

            Assert.IsNull(responseData);
        }

        [Test]
        public void TryDecodeInboundRequestHeaders_ReturnsNull_IfUsingIncorrectKey()
        {
            var headers = new Dictionary<string, string>
            {
                {"WRONG KEY", Strings.Base64Encode("[\"crossProcessId\",\"transactionName\",1.1,2.2,3,null,false]")}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundRequestHeaders(headers, GetHeaderValue);

            Assert.IsNull(responseData);

        }

        [Test]
        public void TryDecodeInboundRequestHeaders_ReturnsNull_IfDataIsNotEncoded()
        {
            var headers = new Dictionary<string, string>
            {
                {TransactionDataHttpHeader, "[\"crossProcessId\",\"transactionName\",1.1,2.2,3,null,false]"}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundRequestHeaders(headers, GetHeaderValue);

            Assert.IsNull(responseData);

        }

        [Test]
        public void TryDecodeInboundRequestHeaders_ReturnsNull_IfDataIsNotFormattedCorrectly()
        {
            var headers = new Dictionary<string, string>
            {
                {"X-NewRelic-ID", Strings.Base64Encode("123")},
                {"X-NewRelic-Transaction", "unexpectedValue"}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundRequestHeaders(headers, GetHeaderValue);

            Assert.IsNull(responseData);

        }

        [Test]
        public void TryDecodeInboundRequestHeaders_ReturnsRequestData_IfHeadersAreValid()
        {
            var headers = new Dictionary<string, string>
            {
                {"X-NewRelic-ID", Strings.Base64Encode("123")},
                {"X-NewRelic-Transaction", Strings.Base64Encode(@"[""guid"", ""false"", ""tripId"", ""pathHash""]")}
            };

            var requestData = _catHeaderHandler.TryDecodeInboundRequestHeaders(headers, GetHeaderValue);

            Assert.NotNull(requestData);
            NrAssert.Multiple
            (
                () => Assert.AreEqual("pathHash", requestData.PathHash),
                () => Assert.AreEqual("guid", requestData.TransactionGuid),
                () => Assert.AreEqual("tripId", requestData.TripId),
                () => Assert.AreEqual(false, requestData.Unused)
            );
        }

        [Test]
        public void TryGetOutboundResponseHeaders_ReturnsEmptyEnumerable_IfCatIsDisabled()
        {
            Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(false);
            var transaction = BuildTestTransaction(pathHash: "pathHash");
            var headers = _catHeaderHandler.TryGetOutboundResponseHeaders(transaction, new TransactionMetricName("WebTransaction", "/foo"));

            Assert.False(headers.Any());
        }

        [Test]
        public void TryGetOutboundResponseHeaders_ReturnsCorrectHeaders()
        {
            Mock.Arrange(() => _configuration.CrossApplicationTracingCrossProcessId).Returns("crossProcessId");
            var refereeTransaction = BuildTestTransaction();
            var refereeTransactionMetricName = new TransactionMetricName("WebTransaction", "foo");
            var headers = _catHeaderHandler.TryGetOutboundResponseHeaders(refereeTransaction, refereeTransactionMetricName).ToDictionary();
            var resultAppDataHttpHeader = Strings.TryBase64Decode(headers[AppDataHttpHeader]);
            var guid = refereeTransaction.Guid;
            var expectedAppDataHttpHeader = $"[\"crossProcessId\",\"WebTransaction/foo\",0.0,{ExpectedResponseTimeInSeconds},-1,\"{guid}\",false]";
            NrAssert.Multiple
            (
                () => Assert.AreEqual(expectedAppDataHttpHeader, resultAppDataHttpHeader)
            );
        }

        [Test]
        public void TryGetOutboundResponseHeaders_EncodesHeadersUsingKeyFromConfig()
        {
            var encodingKey = "key";
            Mock.Arrange(() => _configuration.EncodingKey).Returns(encodingKey);
            Mock.Arrange(() => _configuration.CrossApplicationTracingCrossProcessId).Returns("crossProcessId");
            var refereeTransaction = BuildTestTransaction();
            var refereeTransactionMetricName = new TransactionMetricName("WebTransaction", "foo");
            var headers = _catHeaderHandler.TryGetOutboundResponseHeaders(refereeTransaction, refereeTransactionMetricName).ToDictionary();
            Assert.AreEqual(1, headers.Count);
            var resultAppDataHttpHeader = Strings.TryBase64Decode(headers[AppDataHttpHeader], encodingKey);
            var guid = refereeTransaction.Guid;
            var expectedAppDataHttpHeader = $"[\"crossProcessId\",\"WebTransaction/foo\",0.0,{ExpectedResponseTimeInSeconds},-1,\"{guid}\",false]";
            NrAssert.Multiple
            (
                () => Assert.AreEqual(expectedAppDataHttpHeader, resultAppDataHttpHeader)
            );
        }

        #endregion inbound CAT request - outbound CAT response

        #region oubound CAT request - inbound CAT response

        [Test]
        public void TryGetOutboundRequestHeaders_ReturnsEmptyEnumerable_IfCatIsDisabled()
        {
            Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(false);
            var transaction = BuildTestTransaction(pathHash: "pathHash");
            var headers = _catHeaderHandler.TryGetOutboundRequestHeaders(transaction);

            Assert.False(headers.Any());
        }

        [Test]
        public void TryGetOutboundRequestHeaders_ReturnsCorrectHeaders()
        {
            Mock.Arrange(() => _configuration.CrossApplicationTracingCrossProcessId).Returns("crossProcessId");
            var transaction = BuildTestTransaction(referrerTripId: "referrerTripId", pathHash: "pathHash");

            var headers = _catHeaderHandler.TryGetOutboundRequestHeaders(transaction).ToDictionary();
            var guid = transaction.Guid;
            var resultCrossProcessId = Strings.TryBase64Decode(headers[NewRelicIdHttpHeader]);
            var resultTransactionDataJson = Strings.TryBase64Decode(headers[TransactionDataHttpHeader]);
            var expectedTransactionDataJson = "[\"" + guid + "\",false,\"referrerTripId\",\"pathHash\"]";
            NrAssert.Multiple(
                () => Assert.AreEqual("crossProcessId", resultCrossProcessId),
                () => Assert.AreEqual(expectedTransactionDataJson, resultTransactionDataJson)
                );
        }

        [Test]
        public void TryGetOutboundRequestHeaders_ReturnsCorrectHeaders_IfTripIdIsNull()
        {
            Mock.Arrange(() => _configuration.CrossApplicationTracingCrossProcessId).Returns("crossProcessId");
            var transaction = BuildTestTransaction(pathHash: "pathHash");
            var guid = transaction.Guid;

            var headers = _catHeaderHandler.TryGetOutboundRequestHeaders(transaction).ToDictionary();

            var resultCrossProcessId = Strings.TryBase64Decode(headers[NewRelicIdHttpHeader]);
            var resultTransactionDataJson = Strings.TryBase64Decode(headers[TransactionDataHttpHeader]);
            var expectedTransactionDataJson = "[\"" + guid + "\",false,\"" + guid + "\",\"pathHash\"]";
            NrAssert.Multiple(
                () => Assert.AreEqual("crossProcessId", resultCrossProcessId),
                () => Assert.AreEqual(expectedTransactionDataJson, resultTransactionDataJson)
                );
        }

        [Test]
        public void TryGetOutboundRequestHeaders_EncodesHeadersUsingKeyFromConfig()
        {
            var encodingKey = "key";
            Mock.Arrange(() => _configuration.CrossApplicationTracingCrossProcessId).Returns("crossProcessId");
            Mock.Arrange(() => _configuration.EncodingKey).Returns(encodingKey);
            var transaction = BuildTestTransaction(referrerTripId: "referrerTripId", pathHash: "pathHash");
            var guid = transaction.Guid;
            var headers = _catHeaderHandler.TryGetOutboundRequestHeaders(transaction).ToDictionary();

            Assert.AreEqual(2, headers.Count);

            var resultCrossProcessId = Strings.TryBase64Decode(headers[NewRelicIdHttpHeader], encodingKey);
            var resultTransactionDataJson = Strings.TryBase64Decode(headers[TransactionDataHttpHeader], encodingKey);
            var expectedTransactionDataJson = "[\"" + guid + "\",false,\"referrerTripId\",\"pathHash\"]";
            NrAssert.Multiple(
                () => Assert.AreEqual("crossProcessId", resultCrossProcessId),
                () => Assert.AreEqual(expectedTransactionDataJson, resultTransactionDataJson)
                );
        }

        [Test]
        public void TryDecodeInboundResponseHeaders_ReturnsNull_IfCatIsDisabled()
        {
            Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(false);
            var headers = new Dictionary<string, string>
            {
                {AppDataHttpHeader, Strings.Base64Encode("[\"crossProcessId\",\"transactionName\",1.1,2.2,3,null,false]")}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundResponseHeaders(headers);

            Assert.IsNull(responseData);
        }

        [Test]
        public void TryDecodeInboundResponseHeaders_ReturnsNull_IfUsingIncorrectKey()
        {
            var headers = new Dictionary<string, string>
            {
                {"WRONG KEY", Strings.Base64Encode("[\"crossProcessId\",\"transactionName\",1.1,2.2,3,null,false]")}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundResponseHeaders(headers);

            Assert.IsNull(responseData);
        }

        [Test]
        public void TryDecodeInboundResponseHeaders_ReturnsNull_IfDataIsNotEncoded()
        {
            var headers = new Dictionary<string, string>
            {
                {AppDataHttpHeader, "[\"crossProcessId\",\"transactionName\",1.1,2.2,3,null,false]"}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundResponseHeaders(headers);

            Assert.IsNull(responseData);
        }

        [Test]
        public void TryDecodeInboundResponseHeaders_ReturnsNull_IfDataIsNotFormattedCorrectly()
        {
            var headers = new Dictionary<string, string>
            {
                {AppDataHttpHeader, Strings.Base64Encode("[1]")}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundResponseHeaders(headers);

            Assert.IsNull(responseData);
        }

        [Test]
        public void TryDecodeInboundResponseHeaders_ReturnsResponseData_IfHeadersAreValid()
        {
            var headers = new Dictionary<string, string>
            {
                {AppDataHttpHeader, Strings.Base64Encode("[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\",false]")}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundResponseHeaders(headers);

            Assert.NotNull(responseData);
            NrAssert.Multiple(
                () => Assert.AreEqual("crossProcessId", responseData.CrossProcessId),
                () => Assert.AreEqual("transactionName", responseData.TransactionName),
                () => Assert.AreEqual(1.1f, responseData.QueueTimeInSeconds),
                () => Assert.AreEqual(2.2f, responseData.ResponseTimeInSeconds),
                () => Assert.AreEqual(3, responseData.ContentLength),
                () => Assert.AreEqual("guid", responseData.TransactionGuid),
                () => Assert.AreEqual(false, responseData.Unused)
                );
        }

        [Test]
        public void TryDecodeInboundResponseHeaders_NewRelicResponseHeaderHasMoreThanOneDataBlobSeparateByComma_OnlyDecodeTheFirstDataBlob()
        {
            var firstDataBlob = "[\"crossProcessId1\",\"transactionName1\",1.1,2.2,3,null,false]";
            var secondDataBlob = "[\"crossProcessId2\",\"transactionName2\",1.1,2.2,3,null,false]";

            Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(true);
            var headers = new Dictionary<string, string>
            {
                {AppDataHttpHeader, Strings.Base64Encode(firstDataBlob) + "," + Strings.Base64Encode(secondDataBlob)}
            };

            var responseData = _catHeaderHandler.TryDecodeInboundResponseHeaders(headers);

            Assert.NotNull(responseData);
            Assert.AreEqual("crossProcessId1", responseData.CrossProcessId);
            Assert.AreEqual("transactionName1", responseData.TransactionName);
        }

        #endregion oubound CAT request - inbound CAT response

        #region helpers

        private IInternalTransaction BuildTestTransaction(string pathHash = null, IEnumerable<string> alternatePathHashes = null, string referrerGuid = null, string referrerTripId = null, string referrerPathHash = null, string referrerCrossProcessId = null, string syntheticsResourceId = null, string syntheticsJobId = null, string syntheticsMonitorId = null, bool isSynthetics = false, bool hasCatResponseHeaders = false)
        {
            var name = TransactionName.ForWebTransaction("foo", "bar");
            var startTime = DateTime.Now;
            var duration = TimeSpan.FromSeconds(1);
            pathHash = pathHash ?? "pathHash";
            referrerPathHash = referrerPathHash ?? "referrerPathHash";
            alternatePathHashes = alternatePathHashes ?? Enumerable.Empty<string>();
            referrerGuid = referrerGuid ?? Guid.NewGuid().ToString();
            ISimpleTimer timer = Mock.Create<ISimpleTimer>();
            Mock.Arrange(() => timer.Duration).Returns(duration);

            IAttributeDefinitionService attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            var priority = 0.5f;
            var tx = new Transaction(_configuration, name, timer, startTime, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), priority, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), attribDefSvc.AttributeDefs);
            tx.TransactionMetadata.SetCrossApplicationPathHash(pathHash);
            tx.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid(referrerGuid);
            tx.TransactionMetadata.SetCrossApplicationReferrerTripId(referrerTripId);
            tx.TransactionMetadata.SetCrossApplicationReferrerPathHash(referrerPathHash);
            tx.TransactionMetadata.SetSyntheticsResourceId(syntheticsResourceId);
            tx.TransactionMetadata.SetSyntheticsJobId(syntheticsJobId);
            tx.TransactionMetadata.SetSyntheticsMonitorId(syntheticsMonitorId);
            tx.TransactionMetadata.SetCrossApplicationReferrerProcessId(referrerCrossProcessId);
            tx.TransactionMetadata.SetCrossApplicationResponseTimeInSeconds(ExpectedResponseTimeInSeconds);

            return tx;
        }

        #endregion helpers
    }
}
