// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Metrics;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Api
{
    [TestFixture]
    public class TransactionBridgeApiTests
    {
        private IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;
        private ITransaction _transaction;
        private TransactionBridgeApi _transactionBridgeApi;

        [SetUp]
        public void Setup()
        {
            _apiSupportabilityMetricCounters = Mock.Create<IApiSupportabilityMetricCounters>();
            _transaction = Mock.Create<ITransaction>();

            _transactionBridgeApi = new TransactionBridgeApi(_transaction, _apiSupportabilityMetricCounters, Mock.Create<IConfigurationService>());
        }

        [Test]
        public void SetUserId_AddsSupportabilityMetric()
        {
            _transactionBridgeApi.SetUserId("CustomUserId");

            Mock.Assert(() => _apiSupportabilityMetricCounters.Record(Arg.Matches<ApiMethod>(apiMethod => apiMethod == ApiMethod.SetUserId)));
        }

        [Test]
        public void SetUserId_CallsTransactionSetUserId()
        {
            var expectedCustomUserId = "CustomUserId";
            _transactionBridgeApi.SetUserId(expectedCustomUserId);

            Mock.Assert(() => _transaction.SetUserId(Arg.Matches<string>(userId => userId == expectedCustomUserId)));
        }

        [Test]
        public void SetUserId_HandlesException()
        {
            Mock.Arrange(() => _apiSupportabilityMetricCounters.Record(Arg.IsAny<ApiMethod>())).Throws<Exception>();

            var expectedCustomUserId = "CustomUserId";
            _transactionBridgeApi.SetUserId(expectedCustomUserId);

            // verify we didn't set a UserId
            Mock.Assert(() => _transaction.SetUserId(Arg.AnyString), Occurs.Never());
        }
    }
}
