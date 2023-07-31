// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
#if NETFRAMEWORK
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using System.Linq;

namespace CompositeTests
{
    [TestFixture]
    public class SqlTraceTests
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
        public void SimpleTransaction_CreatesDatastoreTransactionAndSqlTrace()
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0; // Config to run explain plans on queries with any nonzero duration
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

            var sqlTrace = _compositeTestAgent.SqlTraces.First();

            NrAssert.Multiple(
                () => Assert.IsNotNull(sqlTrace),
                () => Assert.AreEqual("Datastore/statement/MSSQL/Table1/SELECT", sqlTrace.DatastoreMetricName),
                () => Assert.AreEqual("SELECT * FROM Table1", sqlTrace.Sql)
            );
        }

        [Test]
        public void SimpleTransaction_CreatesDatastoreTransactionAndSqlTrace_HasQueryParameters()
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0; // Config to run explain plans on queries with any nonzero duration
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

            var sqlTrace = _compositeTestAgent.SqlTraces.First();

            NrAssert.Multiple(
                () => Assert.IsNotNull(sqlTrace),
                () => CollectionAssert.AreEquivalent((Dictionary<string, IConvertible>)sqlTrace.ParameterData["query_parameters"],
                    new Dictionary<string, IConvertible>
                    {
                        {"myKey1", "myValue1"},
                        {"myKey2", "myValue2"}
                    })
            );
        }

        [Test]
        public void SimpleTransaction_CreatesDatastoreTransactionAndSqlTrace_HasNoQueryParameters()
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0; // Config to run explain plans on queries with any nonzero duration
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
                { "myKey1", "myValue1" }
            };

            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1", queryParameters: queryParameters);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var sqlTrace = _compositeTestAgent.SqlTraces.First();

            NrAssert.Multiple(
                () => Assert.IsNotNull(sqlTrace),
                () => Assert.IsFalse(sqlTrace.ParameterData.ContainsKey("query_parameters"))
            );
        }

        [Test]
        public void SimpleTransaction_CreatesDatastoreTransactionAndSqlTrace_NoQueryParameterInput_HasNoQueryParameters()
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0; // Config to run explain plans on queries with any nonzero duration
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

            var sqlTrace = _compositeTestAgent.SqlTraces.First();

            NrAssert.Multiple(
                () => Assert.IsNotNull(sqlTrace),
                () => Assert.IsFalse(sqlTrace.ParameterData.ContainsKey("query_parameters"))
            );
        }

        [Test]
        public void CreatesTransactionAndSqlTrace_RequestUriGloballyExcluded()
        {

            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0; // Config to run explain plans on queries with any nonzero duration
            _compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "request.uri" };
            _compositeTestAgent.PushConfiguration();
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            tx.SetUri("myuri");
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var sqlTrace = _compositeTestAgent.SqlTraces.First();
            Assert.AreEqual("<unknown>", sqlTrace.Uri);
        }

        [Test]
        public void CreatesTransactionAndSqlTrace_RequestUriLocallyExcluded()
        {

            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0; // Config to run explain plans on queries with any nonzero duration
            _compositeTestAgent.LocalConfiguration.transactionTracer.attributes.exclude = new List<string> { "request.uri" };
            _compositeTestAgent.LocalConfiguration.transactionEvents.attributes.exclude = new List<string> { "request.uri" };
            _compositeTestAgent.LocalConfiguration.errorCollector.attributes.exclude = new List<string> { "request.uri" };

            _compositeTestAgent.PushConfiguration();
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            tx.SetUri("myuri");
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var sqlTrace = _compositeTestAgent.SqlTraces.First();
            Assert.AreEqual("myuri", sqlTrace.Uri);
        }

        [Test]
        public void SimpleTransaction_CreatesNoSqlTraceOnFastQuery()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var sqlTrace = _compositeTestAgent.SqlTraces.FirstOrDefault();

            NrAssert.Multiple(
                () => Assert.IsNull(sqlTrace)
            );
        }

        [Test]
        public void SimpleTransaction_CreatesDatastoreTransactionAndExplainPlan()
        {
            var sqlCommand = new SqlCommand();
            var commandText = "SELECT * FROM Table1";
            sqlCommand.CommandText = commandText;

            _compositeTestAgent.LocalConfiguration.transactionTracer.explainEnabled = true;
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0; // Config to run explain plans on queries with any nonzero duration
            _compositeTestAgent.PushConfiguration();
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", commandText);
            _agent.EnableExplainPlans(segment, () => AllocateResources(sqlCommand), GenerateExplainPlan, () => new VendorExplainValidationResult(true));
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var sqlTrace = _compositeTestAgent.SqlTraces.First();
            var explainPlan = (ExplainPlanWireModel)sqlTrace.ParameterData["explain_plan"];
            var explainPlanData = explainPlan.ExplainPlanDatas.First().ToList();
            var transactionSegments = transactionTrace.TransactionTraceData.RootSegment.Children;
            var transactionExplainPlan = (ExplainPlanWireModel)transactionSegments.First().Children.First().Parameters["explain_plan"];

            NrAssert.Multiple(
                () => Assert.IsNotNull(explainPlan),
                () => Assert.AreEqual(commandText, explainPlanData[0].ToString()),
                () => Assert.AreEqual("SELECT", explainPlanData[1].ToString()),
                () => Assert.IsTrue(sqlTrace.ParameterData.ContainsKey("explain_plan")),
                () => Assert.AreEqual(sqlTrace.ParameterData.Values.First(), explainPlan),
                () => Assert.AreEqual(transactionExplainPlan.ExplainPlanDatas, explainPlan.ExplainPlanDatas),
                () => Assert.AreEqual(transactionExplainPlan.ExplainPlanHeaders, explainPlan.ExplainPlanHeaders)
            );
        }

        [Test]
        public void CreatesDatastoreTransactionButNoExplainPlanWhenVendorValidationFails()
        {
            var sqlCommand = new SqlCommand();
            var commandText = "SELECT * FROM Table1";
            sqlCommand.CommandText = commandText;

            _compositeTestAgent.LocalConfiguration.transactionTracer.explainEnabled = true;
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0; // Config to run explain plans on queries with any nonzero duration
            _compositeTestAgent.PushConfiguration();
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", commandText);
            _agent.EnableExplainPlans(segment, () => AllocateResources(sqlCommand), GenerateExplainPlan, () => new VendorExplainValidationResult(false));
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var sqlTrace = _compositeTestAgent.SqlTraces.First();
            var transactionSegments = transactionTrace.TransactionTraceData.RootSegment.Children;
            var transactionTraceSegmentParameters = transactionSegments.First().Children.First().Parameters;

            NrAssert.Multiple(
                () => Assert.IsFalse(sqlTrace.ParameterData.ContainsKey("explain_plan")),
                () => Assert.IsFalse(transactionTraceSegmentParameters.ContainsKey("explain_plan"))
            );
        }

        private object AllocateResources(IDbCommand command)
        {
            return command;
        }

        private ExplainPlan GenerateExplainPlan(object resources)
        {
            if (!(resources is IDbCommand))
                return null;

            var dbCommand = (IDbCommand)resources;
            var explainPlanHeaders = new List<string>(new[] { "StmtText", "Type" });
            var explainPlanDatas = new List<List<object>>();
            var explainPlan = new List<object>(new object[] { dbCommand.CommandText, "SELECT" });
            explainPlanDatas.Add(explainPlan);
            var obfuscatedHeaders = new List<int>(new[] { 0, 1 });
            return new ExplainPlan(explainPlanHeaders, explainPlanDatas, obfuscatedHeaders);
        }
    }
}
