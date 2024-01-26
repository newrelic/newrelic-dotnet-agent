// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;

namespace CompositeTests
{
    public class CATSupportabilityMetricTests
    {

        private static readonly long _accountId = 123;
        private static readonly long _appId = 5678;
        private static readonly string _trustKey = "trustedkey";

        private const string NewRelicIdHttpHeader = "X-NewRelic-ID";
        private const string TransactionDataHttpHeader = "X-NewRelic-Transaction";
        private const string AppDataHttpHeader = "X-NewRelic-App-Data";

        private CompositeTestAgent _compositeTestAgent;
        private IAgent _agent => _compositeTestAgent.GetAgent();

        private ICATSupportabilityMetricCounters _catMetricCounters;

        private string _crossProcessIDEncoded;
        private string _reqDataEncoded;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _compositeTestAgent.LocalConfiguration.distributedTracing.enabled = false;
            _compositeTestAgent.LocalConfiguration.crossApplicationTracingEnabled = true;
            _compositeTestAgent.ServerConfiguration.AccountId = _accountId.ToString();
            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = _trustKey;
            _compositeTestAgent.ServerConfiguration.PrimaryApplicationId = _appId.ToString();
            _catMetricCounters = _compositeTestAgent.Container.Resolve<ICATSupportabilityMetricCounters>();

            var crossProcessID = $"{_accountId}#{_appId}";
            _crossProcessIDEncoded = Strings.Base64Encode(crossProcessID, _agent.Configuration.EncodingKey);

            var reqData = new CrossApplicationRequestData("referrerTransactionGuid", false, "referrerTripId", "referrerPathHash");
            _reqDataEncoded = HeaderEncoder.SerializeAndEncode(reqData, _agent.Configuration.EncodingKey);

            _compositeTestAgent.ServerConfiguration.TrustedIds = new long[] { _accountId };

            _compositeTestAgent.PushConfiguration();
        }


        [TearDown]
        public void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void SupportabilityMetric_Request_Accept_Success()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader,_crossProcessIDEncoded),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Accept_Success),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1)
            );

        }

        [Test]
        public void SupportabilityMetric_Request_Accept_Duplicate_SameXProcessID()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader,_crossProcessIDEncoded),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);
            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, CATSupportabilityCondition.Request_Accept_Multiple),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Multiple, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Request_Accept_Duplicate_DifferentXProcessID()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var goodRequestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader,_crossProcessIDEncoded),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            var badCrossProcessID = Strings.Base64Encode("1111:2222", _agent.Configuration.EncodingKey);
            var badRequestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader,badCrossProcessID),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(goodRequestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(badRequestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, CATSupportabilityCondition.Request_Accept_Multiple),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Multiple, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Request_Accept_Failure_Exception()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (cond == CATSupportabilityCondition.Request_Accept_Success)
                    {
                        throw new Exception("Test Exception");
                    }

                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader,_crossProcessIDEncoded),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Accept_Failure),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Failure, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Request_Accept_Failure_NotTrusted()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var notTrustedXprocesID = Strings.Base64Encode("1111:2222", _agent.Configuration.EncodingKey);
            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader, notTrustedXprocesID),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Accept_Failure_NotTrusted),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Failure_NotTrusted, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Request_Accept_Failure_Decode_RequestData()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var badlyEncodedData = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader,_crossProcessIDEncoded),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, "This isn't encoded properly")
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(badlyEncodedData, HeaderFunctions.GetHeaders, TransportType.HTTP);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Accept_Failure_Decode),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Failure_Decode, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Request_Accept_Failure_Decode_CrossProcessID()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var badlyEncodedData = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader, "This isn't encoded properly"),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(badlyEncodedData, HeaderFunctions.GetHeaders, TransportType.HTTP);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Accept_Failure_Decode),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Failure_Decode, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Request_Create_Success()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            trx.GetRequestMetadata();

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Create_Success),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Create_Success, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Request_Create_Failure_CrossProcessID()
        {

            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });


            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            trx.GetRequestMetadata();

            _compositeTestAgent.ServerConfiguration.CatId = null;

            trx.GetRequestMetadata();

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Create_Failure_XProcID, CATSupportabilityCondition.Request_Create_Success),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Create_Failure_XProcID, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Create_Success, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Request_Create_Failure_Exception()
        {

            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (cond == CATSupportabilityCondition.Request_Create_Success)
                    {
                        throw new Exception("Test Exception");
                    }

                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            trx.GetRequestMetadata();

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Create_Failure),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Create_Failure, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Response_Create_Success()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader,_crossProcessIDEncoded),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            trx.GetResponseMetadata();

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, CATSupportabilityCondition.Response_Create_Success),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Create_Success, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Response_Create_Failure_CrossProcessID()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader,_crossProcessIDEncoded),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            trx.GetResponseMetadata();
            _compositeTestAgent.ServerConfiguration.CatId = null;
            trx.GetResponseMetadata();

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts,
                        CATSupportabilityCondition.Request_Accept_Success,
                        CATSupportabilityCondition.Response_Create_Success,
                        CATSupportabilityCondition.Response_Create_Failure_XProcID),

                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Create_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Create_Failure_XProcID, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Response_Create_Failure_Exception()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (cond == CATSupportabilityCondition.Response_Create_Success)
                    {
                        throw new Exception("Test Exception");
                    }

                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var trx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(NewRelicIdHttpHeader,_crossProcessIDEncoded),
                new KeyValuePair<string, string>(TransactionDataHttpHeader, _reqDataEncoded)
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            trx.GetResponseMetadata();

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts,
                        CATSupportabilityCondition.Request_Accept_Success,
                        CATSupportabilityCondition.Response_Create_Failure),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Create_Failure, 1)
            );
        }



        [Test]
        public void SupportabilityMetric_Response_Accept_Success()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var clientTrx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "client",
                doNotTrackAsUnitOfWork: true);
            var clientSegment = _agent.StartExternalRequestSegmentOrThrow(new Uri("http://test"), "get");


            //Create the request Metadata
            var requestMetadata = clientTrx.GetRequestMetadata();

            var svcTrx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "service",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestMetadata, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var responseMetaData = svcTrx.GetResponseMetadata();

            clientTrx.ProcessInboundResponse(responseMetaData, clientSegment);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts,
                CATSupportabilityCondition.Request_Create_Success,
                CATSupportabilityCondition.Request_Accept_Success,
                CATSupportabilityCondition.Response_Create_Success,
                CATSupportabilityCondition.Response_Accept_Success),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Create_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Create_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Accept_Success, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Response_Accept_MultipleResponses()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var clientTrx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "client",
                doNotTrackAsUnitOfWork: true);
            var clientSegment = _agent.StartExternalRequestSegmentOrThrow(new Uri("http://test"), "get");

            //Create the request Metadata
            var requestMetadata = clientTrx.GetRequestMetadata();

            var svcTrx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "service",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestMetadata, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var responseMetaData = svcTrx.GetResponseMetadata().FirstOrDefault();

            var dupResponseMetaData = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string,string>(responseMetaData.Key, responseMetaData.Value + "," + responseMetaData.Value)
            };

            clientTrx.ProcessInboundResponse(dupResponseMetaData, clientSegment);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts,
                CATSupportabilityCondition.Request_Create_Success,
                CATSupportabilityCondition.Request_Accept_Success,
                CATSupportabilityCondition.Response_Create_Success,
                CATSupportabilityCondition.Response_Accept_MultipleResponses,
                CATSupportabilityCondition.Response_Accept_Success),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Create_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Create_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Request_Accept_Success, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Accept_MultipleResponses, 1),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Accept_Success, 1)
            );
        }

        [Test]
        public void SupportabilityMetric_Response_Accept_Exception()
        {
            //Collect the different metric counts in a dictionary that we can use
            var conditionCounts = new Dictionary<CATSupportabilityCondition, int>();
            Mock.Arrange(() => _catMetricCounters.Record(Arg.IsAny<CATSupportabilityCondition>()))
                .DoInstead<CATSupportabilityCondition>((cond) =>
                {
                    if (!conditionCounts.ContainsKey(cond))
                    {
                        conditionCounts.Add(cond, 0);
                    }
                    conditionCounts[cond]++;
                });

            var clientTrx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "client",
                doNotTrackAsUnitOfWork: true);
            var clientSegment = _agent.StartExternalRequestSegmentOrThrow(new Uri("http://test"), "get");

            var dupResponseMetaData = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string,string>("X-NewRelic-App-Data", "123456"),
            };

            clientTrx.ProcessInboundResponse(dupResponseMetaData, clientSegment);

            NrAssert.Multiple
            (
                () => TestExistenceOfConditions(conditionCounts, CATSupportabilityCondition.Response_Accept_Failure),
                () => TestConditionValue(conditionCounts, CATSupportabilityCondition.Response_Accept_Failure, 1));
        }

        private void TestExistenceOfConditions(Dictionary<CATSupportabilityCondition, int> dic, params CATSupportabilityCondition[] conditions)
        {
            var uniqueTestConditions = conditions.Distinct().ToArray();
            var actualConditions = dic.Keys.ToArray();


            var missingConditions = uniqueTestConditions.Except(actualConditions).ToArray();
            var unexpectedConditions = actualConditions.Except(uniqueTestConditions).ToArray();

            NrAssert.Multiple(

                () => Assert.That(missingConditions, Is.Empty, $"The following expected conditions were not captured: {string.Join(", ", missingConditions)}"),
                () => Assert.That(unexpectedConditions, Is.Empty, $"The following unexpected conditions were detected: {string.Join(", ", unexpectedConditions)}")
            );
        }

        private void TestConditionValue(Dictionary<CATSupportabilityCondition, int> dic, CATSupportabilityCondition condition, int expectedValue)
        {
            Assert.Multiple(() =>
            {
                Assert.That(dic.ContainsKey(condition), Is.True, $"Unable To find {condition} in result");
                Assert.That(dic[condition], Is.EqualTo(expectedValue), $"Count Mismatch - {condition} - Expected {expectedValue}, Actual {dic[condition]}");
            });
        }
    }
}
