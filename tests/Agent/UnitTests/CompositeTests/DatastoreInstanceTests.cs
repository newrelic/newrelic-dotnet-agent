// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;

namespace CompositeTests
{
    [TestFixture]
    public class DatastoreInstanceTests
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

        #region SQL

        [Test]
        public void DatastoreInstance_AllAttributes_OnSqlTrace_When_InstanceReportingIsEnabled_And_DatabaseNameReportingIsEnabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(true, true);

            var sqlTrace = _compositeTestAgent.SqlTraces.First();

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("myhost", sqlTrace.ParameterData["host"]),
                () => ClassicAssert.AreEqual("myport", sqlTrace.ParameterData["port_path_or_id"]),
                () => ClassicAssert.AreEqual("mydatabase", sqlTrace.ParameterData["database_name"])
            );
        }

        [Test]
        public void DatastoreInstance_DatabaseName_OnSqlTrace_WhenInstanceReportingIsDisabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(false, true);

            var sqlTrace = _compositeTestAgent.SqlTraces.First();

            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(!sqlTrace.ParameterData.ContainsKey("host")),
                () => ClassicAssert.IsTrue(!sqlTrace.ParameterData.ContainsKey("port_path_or_id")),
                () => ClassicAssert.AreEqual("mydatabase", sqlTrace.ParameterData["database_name"])
            );
        }

        [Test]
        public void DatastoreInstance_Host_PortPathOrId_OnSqlTrace_When_InstanceReportingIsEnabled_And_DatabaseNameReportingIsDisabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(true, false);

            var sqlTrace = _compositeTestAgent.SqlTraces.First();

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("myhost", sqlTrace.ParameterData["host"]),
                () => ClassicAssert.AreEqual("myport", sqlTrace.ParameterData["port_path_or_id"]),
                () => ClassicAssert.IsTrue(!sqlTrace.ParameterData.ContainsKey("database_name"))
            );
        }

        [Test]
        public void DatastoreInstance_NoAttributes_OnSqlTrace_When_InstanceReportingIsDisabled_And_DatabaseNameReportingIsDisabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(false, false);

            var sqlTrace = _compositeTestAgent.SqlTraces.First();

            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(!sqlTrace.ParameterData.ContainsKey("host")),
                () => ClassicAssert.IsTrue(!sqlTrace.ParameterData.ContainsKey("port_path_or_id")),
                () => ClassicAssert.IsTrue(!sqlTrace.ParameterData.ContainsKey("database_name"))
            );
        }

        [Test]
        public void DatastoreInstance_AllAttributes_OnTransactionTrace_WhenInstanceReportingIsEnabled_And_WhenDatabaseNameReportingIsEnabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(true, true);

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("myhost", parameters["host"]),
                () => ClassicAssert.AreEqual("myport", parameters["port_path_or_id"]),
                () => ClassicAssert.AreEqual("mydatabase", parameters["database_name"])
            );
        }

        [Test]
        public void DatastoreInstance_DatabaseName_OnTransactionTrace_WhenInstanceReportingIsDisabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(false, true);

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("host")),
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("port_path_or_id")),
                () => ClassicAssert.AreEqual("mydatabase", parameters["database_name"])
            );
        }

        [Test]
        public void DatastoreInstance_Host_PortPathOrId_OnTransactionTrace_WhenDatabaseNameReportingIsDisabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(true, false);

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("myhost", parameters["host"]),
                () => ClassicAssert.AreEqual("myport", parameters["port_path_or_id"]),
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("database_name"))
            );
        }

        [Test]
        public void DatastoreInstance_NoAttributes_OnTransactionTrace_WhenInstanceReportingIsDisabled_And_WhenDatabaseNameReportingIsDisabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(false, false);

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("host")),
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("port_path_or_id")),
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("database_name"))
            );
        }

        private void CreateATransactionWithDatastoreSegmentAndHarvest(bool instanceReportingEnabled, bool databaseNameReportingEnabled, DatastoreVendor vendor = DatastoreVendor.MSSQL, string host = "myhost", string portPathOrId = "myport", string databaseName = "mydatabase")
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
            _compositeTestAgent.LocalConfiguration.datastoreTracer.instanceReporting.enabled = instanceReportingEnabled;
            _compositeTestAgent.LocalConfiguration.datastoreTracer.databaseNameReporting.enabled = databaseNameReportingEnabled;
            _compositeTestAgent.PushConfiguration();
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", vendor, "Table1", "SELECT * FROM Table1", null, host, portPathOrId, databaseName);
            segment.End();
            tx.End();
            _compositeTestAgent.Harvest();
        }

        #endregion SQL

        #region Redis

        [Test]
        public void DatastoreInstance_Redis_AllAttributes_OnTransactionTrace_WhenInstanceReportingIsEnabled_And_WhenDatabaseNameReportingIsEnabled()
        {
            CreateATransactionWithStackExchangeRedisDatastoreSegmentAndHarvest(true, true);

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("myhost", parameters["host"]),
                () => ClassicAssert.AreEqual("myport", parameters["port_path_or_id"]),
                () => ClassicAssert.AreEqual("mydatabase", parameters["database_name"])
            );
        }

        [Test]
        public void DatastoreInstance_Redis_DatabaseName_OnTransactionTrace_WhenInstanceReportingIsDisabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(false, true);

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("host")),
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("port_path_or_id")),
                () => ClassicAssert.AreEqual("mydatabase", parameters["database_name"])
            );
        }

        [Test]
        public void DatastoreInstance_Redis_Host_PortPathOrId_OnTransactionTrace_WhenDatabaseNameReportingIsDisabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(true, false);

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("myhost", parameters["host"]),
                () => ClassicAssert.AreEqual("myport", parameters["port_path_or_id"]),
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("database_name"))
            );
        }

        [Test]
        public void DatastoreInstance_Redis_NoAttributes_OnTransactionTrace_WhenInstanceReportingIsDisabled_And_WhenDatabaseNameReportingIsDisabled()
        {
            CreateATransactionWithDatastoreSegmentAndHarvest(false, false);

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("host")),
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("port_path_or_id")),
                () => ClassicAssert.IsTrue(!parameters.ContainsKey("database_name"))
            );
        }

        private void CreateATransactionWithStackExchangeRedisDatastoreSegmentAndHarvest(bool instanceReportingEnabled, bool databaseNameReportingEnabled, DatastoreVendor vendor = DatastoreVendor.Redis, string host = "myhost", string portPathOrId = "myport", string databaseName = "mydatabase")
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
            _compositeTestAgent.LocalConfiguration.datastoreTracer.instanceReporting.enabled = instanceReportingEnabled;
            _compositeTestAgent.LocalConfiguration.datastoreTracer.databaseNameReporting.enabled = databaseNameReportingEnabled;
            _compositeTestAgent.PushConfiguration();
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartStackExchangeRedisDatastoreRequestSegmentOrThrow("GET", vendor, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200), null, host, portPathOrId, databaseName);
            segment.End();
            tx.End();
            _compositeTestAgent.Harvest();
        }

        #endregion Redis
    }
}
