// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Segments.Tests;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class DatastoreSegmentTransformerTests
    {

        private IConfigurationService _configurationService;
        private IDatabaseService _databaseService;

        [SetUp]
        public void SetUp()
        {
            _configurationService = Mock.Create<IConfigurationService>();
            _databaseService = new DatabaseService();
        }

        #region Transform

        [Test]
        public void TransformSegment_NullStats()
        {

            var wrapperVendor = Extensions.Providers.Wrapper.DatastoreVendor.MSSQL;
            var model = "MY_TABLE";
            var operation = "INSERT";

            var segment = GetSegment(wrapperVendor, operation, model);

            //make sure it does not throw
            segment.AddMetricStats(null, _configurationService);

        }

        [Test]
        public void TransformSegment_CreatesWebSegmentMetrics()
        {
            var wrapperVendor = Extensions.Providers.Wrapper.DatastoreVendor.MSSQL;
            var model = "MY_TABLE";
            var operation = "INSERT";

            var segment = GetSegment(wrapperVendor, operation, model, 5);


            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            segment.AddMetricStats(txStats, _configurationService);


            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(6, unscoped.Count);

            const string statementMetric = "Datastore/statement/MSSQL/MY_TABLE/INSERT";
            const string operationMetric = "Datastore/operation/MSSQL/INSERT";
            Assert.IsTrue(unscoped.ContainsKey("Datastore/all"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/allWeb"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/MSSQL/all"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/MSSQL/allWeb"));
            Assert.IsTrue(unscoped.ContainsKey(statementMetric));
            Assert.IsTrue(unscoped.ContainsKey(operationMetric));

            Assert.IsTrue(scoped.ContainsKey(statementMetric));

            var data = scoped[statementMetric];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            var unscopedMetricsWithExclusiveTime = new string[] { statementMetric, operationMetric, "Datastore/all", "Datastore/allWeb", "Datastore/MSSQL/all", "Datastore/MSSQL/allWeb" };

            foreach (var current in unscopedMetricsWithExclusiveTime)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
            }
        }

        [Test]
        public void TransformSegment_CreatesOtherSegmentMetrics()
        {
            var wrapperVendor = Extensions.Providers.Wrapper.DatastoreVendor.MSSQL;
            var model = "MY_TABLE";
            var operation = "INSERT";

            var segment = GetSegment(wrapperVendor, operation, model, 5);


            var txName = new TransactionMetricName("OtherTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(6, unscoped.Count);

            const string statementMetric = "Datastore/statement/MSSQL/MY_TABLE/INSERT";
            const string operationMetric = "Datastore/operation/MSSQL/INSERT";
            Assert.IsTrue(unscoped.ContainsKey("Datastore/all"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/allOther"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/MSSQL/all"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/MSSQL/allOther"));
            Assert.IsTrue(unscoped.ContainsKey(statementMetric));
            Assert.IsTrue(unscoped.ContainsKey(operationMetric));

            Assert.IsTrue(scoped.ContainsKey(statementMetric));

            var data = scoped[statementMetric];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            var unscopedMetricsWithExclusiveTime = new string[] { statementMetric, operationMetric, "Datastore/all", "Datastore/allOther", "Datastore/MSSQL/all", "Datastore/MSSQL/allOther" };

            foreach (var current in unscopedMetricsWithExclusiveTime)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
            }
        }

        [Test]
        public void TransformSegment_CreatesNullModelSegmentMetrics()
        {
            var wrapperVendor = Extensions.Providers.Wrapper.DatastoreVendor.MSSQL;
            var model = null as string;
            var operation = "INSERT";

            var segment = GetSegment(wrapperVendor, operation, model, 5);


            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);


            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(5, unscoped.Count);

            //no statement metric for null model
            const string statementMetric = "Datastore/statement/MSSQL/MY_TABLE/INSERT";
            const string operationMetric = "Datastore/operation/MSSQL/INSERT";
            Assert.IsTrue(unscoped.ContainsKey("Datastore/all"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/allWeb"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/MSSQL/all"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/MSSQL/allWeb"));
            Assert.IsFalse(unscoped.ContainsKey(statementMetric));
            Assert.IsTrue(unscoped.ContainsKey(operationMetric));

            Assert.IsTrue(scoped.ContainsKey(operationMetric));

            var data = scoped[operationMetric];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            var unscopedMetricsWithExclusiveTime = new string[] { operationMetric, "Datastore/all", "Datastore/allWeb", "Datastore/MSSQL/all", "Datastore/MSSQL/allWeb" };

            foreach (var current in unscopedMetricsWithExclusiveTime)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
            }
        }

        [Test]
        public void TransformSegment_CreatesInstanceMetrics()
        {
            var wrapperVendor = Extensions.Providers.Wrapper.DatastoreVendor.MSSQL;
            var model = "MY_TABLE";
            var operation = "INSERT";
            var host = "HOST";
            var portPathOrId = "8080";

            var segment = GetSegment(wrapperVendor, operation, model, 5, host: host, portPathOrId: portPathOrId);

            var txName = new TransactionMetricName("OtherTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            Mock.Arrange(() => _configurationService.Configuration.InstanceReportingEnabled).Returns(true);

            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(7, unscoped.Count);

            const string statementMetric = "Datastore/statement/MSSQL/MY_TABLE/INSERT";
            const string operationMetric = "Datastore/operation/MSSQL/INSERT";
            const string instanceMetric = "Datastore/instance/MSSQL/HOST/8080";
            Assert.IsTrue(unscoped.ContainsKey("Datastore/all"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/allOther"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/MSSQL/all"));
            Assert.IsTrue(unscoped.ContainsKey("Datastore/MSSQL/allOther"));
            Assert.IsTrue(unscoped.ContainsKey(statementMetric));
            Assert.IsTrue(unscoped.ContainsKey(operationMetric));
            Assert.IsTrue(unscoped.ContainsKey(instanceMetric));

            Assert.IsTrue(scoped.ContainsKey(statementMetric));

            var data = scoped[statementMetric];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            var unscopedMetricsWithExclusiveTime = new string[] { statementMetric, operationMetric, "Datastore/all", "Datastore/allOther", "Datastore/MSSQL/all", "Datastore/MSSQL/allOther" };

            foreach (var current in unscopedMetricsWithExclusiveTime)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
            }

            data = unscoped[instanceMetric];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);
        }
        #endregion Transform

        private Segment GetSegment(DatastoreVendor vendor, string operation, string model)
        {
            var data = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(vendor, model, operation));
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            segment.SetSegmentData(data);

            return segment;
        }

        private Segment GetSegment(DatastoreVendor vendor, string operation, string model, double duration, CrossApplicationResponseData catResponseData = null, string host = null, string portPathOrId = null)
        {
            var methodCallData = new MethodCallData("foo", "bar", 1);
            var data = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(vendor, model, operation), null, new ConnectionInfo("none", host, portPathOrId, null));
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), methodCallData);
            segment.SetSegmentData(data);

            return segment.CreateSimilar(new TimeSpan(), TimeSpan.FromSeconds(duration), null);
        }
    }
}
