// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace CompositeTests
{
    [TestFixture]
    public class SqlTraceTests
    {
        private static CompositeTestAgent _compositeTestAgent;
        private IAgentWrapperApi _agentWrapperApi;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();
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
            using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                var segment = _agentWrapperApi.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1");
                segment.End();
            }

            _compositeTestAgent.Harvest();

            var sqlTrace = _compositeTestAgent.SqlTraces.FirstOrDefault();

            NrAssert.Multiple(
                () => Assert.IsNotNull(sqlTrace),
                () => Assert.AreEqual("Datastore/statement/MSSQL/Table1/SELECT", sqlTrace.DatastoreMetricName),
                () => Assert.AreEqual("SELECT * FROM Table1", sqlTrace.Sql)
            );
        }

        [Test]
        public void SimpleTransaction_CreatesNoSqlTraceOnFastQuery()
        {
            using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                var segment = _agentWrapperApi.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1");
                segment.End();
            }

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
            using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                var segment = _agentWrapperApi.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", commandText);
                _agentWrapperApi.EnableExplainPlans(segment, () => AllocateResources(sqlCommand), GenerateExplainPlan, () => new VendorExplainValidationResult(true));
                segment.End();
            }
            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var sqlTrace = _compositeTestAgent.SqlTraces.First();
            var explainPlan = (ExplainPlanWireModel)sqlTrace.ParameterData["explain_plan"];
            var explainPlanData = explainPlan.ExplainPlanDatas.First().ToList();
            var transactionSegments = transactionTrace.TransactionTraceData.RootSegment.Children;
            var transactionExplainPlan = (ExplainPlanWireModel)transactionSegments.First().Children.First().Parameters.Values.First();

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
            using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                var segment = _agentWrapperApi.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", commandText);
                _agentWrapperApi.EnableExplainPlans(segment, () => AllocateResources(sqlCommand), GenerateExplainPlan, () => new VendorExplainValidationResult(false));
                segment.End();
            }
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
            var explainPlanHeaders = new List<string>(new string[] { "StmtText", "Type" });
            var explainPlanDatas = new List<List<object>>();
            var explainPlan = new List<object>(new object[] { dbCommand.CommandText, "SELECT" });
            explainPlanDatas.Add(explainPlan);
            var obfuscatedHeaders = new List<int>(new int[] { 0, 1 });
            return new ExplainPlan(explainPlanHeaders, explainPlanDatas, obfuscatedHeaders);
        }
    }
}
