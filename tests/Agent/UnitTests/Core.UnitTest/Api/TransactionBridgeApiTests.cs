// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
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

        [Test]
        public void StartDatastoreSegment_AddsSupportabilityMetric()
        {
            _transactionBridgeApi.StartDatastoreSegment("Vendor", "Model", "Operation", "CommandText", "Host", "PortPathOrID", "DatabaseName");

            Mock.Assert(() => _apiSupportabilityMetricCounters.Record(Arg.Matches<ApiMethod>(apiMethod => apiMethod == ApiMethod.StartDatastoreSegment)));
        }

        [Test]
        public void StartDatastoreSegment_AllValues_CallsTransactionStartDatastoreSegment()
        {
            var expectedVendor = "Vendor";
            var expectedModel = "Model";
            var expectedOperation = "Operation";
            var expectedCommandText = "CommandText";
            var expectedHost = "Host";
            var expectedPortPathOrID = "PortPathOrID";
            var expectedDatabaseName = "DatabaseName";

            _transactionBridgeApi.StartDatastoreSegment(expectedVendor, expectedModel, expectedOperation, expectedCommandText, expectedHost, expectedPortPathOrID, expectedDatabaseName);

            Mock.Assert(() => _transaction.StartDatastoreSegment(
                Arg.Matches<MethodCall>(methodCall =>
                    methodCall.Method.Type == typeof(object)
                    && methodCall.Method.MethodName == "StartDatastoreSegment"
                    && methodCall.Method.ParameterTypeNames == string.Empty),
                Arg.Matches<ParsedSqlStatement>(statement =>
                    statement.DatastoreVendor == DatastoreVendor.Other
                    && statement.Model == expectedModel
                    && statement.Operation == expectedOperation),
                Arg.Matches<ConnectionInfo>(info =>
                    info.Host == expectedHost
                    && info.PortPathOrId == expectedPortPathOrID
                    && info.DatabaseName == expectedDatabaseName),
                Arg.Matches<string>(commandText => commandText == expectedCommandText),
                Arg.IsNull<IDictionary<string, IConvertible>>(),
                Arg.Matches<bool>(isLeaf => isLeaf == false)
            ));
        }

        [Test]
        public void StartDatastoreSegment_HandlesException()
        {
            Mock.Arrange(() => _apiSupportabilityMetricCounters.Record(Arg.IsAny<ApiMethod>())).Throws<Exception>();

            var expectedVendor = "Vendor";
            var expectedModel = "Model";
            var expectedOperation = "Operation";
            var expectedCommandText = "CommandText";
            var expectedHost = "Host";
            var expectedPortPathOrID = "PortPathOrID";
            var expectedDatabaseName = "DatabaseName";

            _transactionBridgeApi.StartDatastoreSegment(expectedVendor, expectedModel, expectedOperation, expectedCommandText, expectedHost, expectedPortPathOrID, expectedDatabaseName);

            // verify we didn't start a datastore segment
            Mock.Assert(() => _transaction.StartDatastoreSegment(Arg.IsAny<MethodCall>(), Arg.IsAny<ParsedSqlStatement>(), Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>(), Arg.IsAny<IDictionary<string, IConvertible>>(), Arg.IsAny<bool>()), Occurs.Never());
        }
    }
}
