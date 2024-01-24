// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompositeTests
{
    [TestFixture]
    public class DatastoreTransactionTraceTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void TransactionTrace_HasSqlParameter()
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            Assert.That(parameters["sql"], Is.EqualTo("SELECT * FROM Table1"));
        }

        [Test]
        public void TransactionTrace_HasQueryParameters()
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
            _compositeTestAgent.LocalConfiguration.transactionTracer.recordSql = configurationTransactionTracerRecordSql.raw;
            _compositeTestAgent.LocalConfiguration.datastoreTracer.queryParameters.enabled = true;
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var queryParameters = new Dictionary<string, IConvertible>
            {
                {"myKey1", "myValue1"},
                {"myKey2", "myValue2"}
            };
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1", queryParameters: queryParameters);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
            Assert.That(transactionTrace, Is.Not.Null);

            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            Assert.Multiple(() =>
            {
                Assert.That(parameters.ContainsKey("query_parameters"), Is.True);
                Assert.That(new Dictionary<string, IConvertible>
            {
                {"myKey1", "myValue1"},
                {"myKey2", "myValue2"}
            }, Is.EquivalentTo((Dictionary<string, IConvertible>)parameters["query_parameters"]));
            });
        }

        [Test]
        public void TransactionTrace_HasNoQueryParameters()
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
            _compositeTestAgent.LocalConfiguration.transactionTracer.recordSql = configurationTransactionTracerRecordSql.raw;
            _compositeTestAgent.LocalConfiguration.datastoreTracer.queryParameters.enabled = false;
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var queryParameters = new Dictionary<string, IConvertible>
            {
                {"myKey", "myValue"}
            };
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1", queryParameters: queryParameters);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
            Assert.That(transactionTrace, Is.Not.Null);

            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            Assert.That(parameters.ContainsKey("query_parameters"), Is.False);
        }

        [Test]
        public void TransactionTrace_NoQueryParameterInput_HasNoQueryParameters()
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
            _compositeTestAgent.LocalConfiguration.transactionTracer.recordSql = configurationTransactionTracerRecordSql.raw;
            _compositeTestAgent.LocalConfiguration.datastoreTracer.queryParameters.enabled = true;
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
            Assert.That(transactionTrace, Is.Not.Null);

            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            Assert.That(parameters.ContainsKey("query_parameters"), Is.False);
        }
    }
}
