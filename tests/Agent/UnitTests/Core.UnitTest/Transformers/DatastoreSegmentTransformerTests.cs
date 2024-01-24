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

        [TearDown]
        public void TearDown()
        {
            _databaseService.Dispose();
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(6));
            });

            const string statementMetric = "Datastore/statement/MSSQL/MY_TABLE/INSERT";
            const string operationMetric = "Datastore/operation/MSSQL/INSERT";
            Assert.Multiple(() =>
            {
                Assert.That(unscoped.ContainsKey("Datastore/all"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/allWeb"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/MSSQL/all"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/MSSQL/allWeb"), Is.True);
                Assert.That(unscoped.ContainsKey(statementMetric), Is.True);
                Assert.That(unscoped.ContainsKey(operationMetric), Is.True);

                Assert.That(scoped.ContainsKey(statementMetric), Is.True);
            });

            var data = scoped[statementMetric];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            var unscopedMetricsWithExclusiveTime = new string[] { statementMetric, operationMetric, "Datastore/all", "Datastore/allWeb", "Datastore/MSSQL/all", "Datastore/MSSQL/allWeb" };

            foreach (var current in unscopedMetricsWithExclusiveTime)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(6));
            });

            const string statementMetric = "Datastore/statement/MSSQL/MY_TABLE/INSERT";
            const string operationMetric = "Datastore/operation/MSSQL/INSERT";
            Assert.Multiple(() =>
            {
                Assert.That(unscoped.ContainsKey("Datastore/all"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/allOther"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/MSSQL/all"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/MSSQL/allOther"), Is.True);
                Assert.That(unscoped.ContainsKey(statementMetric), Is.True);
                Assert.That(unscoped.ContainsKey(operationMetric), Is.True);

                Assert.That(scoped.ContainsKey(statementMetric), Is.True);
            });

            var data = scoped[statementMetric];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            var unscopedMetricsWithExclusiveTime = new string[] { statementMetric, operationMetric, "Datastore/all", "Datastore/allOther", "Datastore/MSSQL/all", "Datastore/MSSQL/allOther" };

            foreach (var current in unscopedMetricsWithExclusiveTime)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(5));
            });

            //no statement metric for null model
            const string statementMetric = "Datastore/statement/MSSQL/MY_TABLE/INSERT";
            const string operationMetric = "Datastore/operation/MSSQL/INSERT";
            Assert.Multiple(() =>
            {
                Assert.That(unscoped.ContainsKey("Datastore/all"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/allWeb"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/MSSQL/all"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/MSSQL/allWeb"), Is.True);
                Assert.That(unscoped.ContainsKey(statementMetric), Is.False);
                Assert.That(unscoped.ContainsKey(operationMetric), Is.True);

                Assert.That(scoped.ContainsKey(operationMetric), Is.True);
            });

            var data = scoped[operationMetric];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            var unscopedMetricsWithExclusiveTime = new string[] { operationMetric, "Datastore/all", "Datastore/allWeb", "Datastore/MSSQL/all", "Datastore/MSSQL/allWeb" };

            foreach (var current in unscopedMetricsWithExclusiveTime)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(7));
            });

            const string statementMetric = "Datastore/statement/MSSQL/MY_TABLE/INSERT";
            const string operationMetric = "Datastore/operation/MSSQL/INSERT";
            const string instanceMetric = "Datastore/instance/MSSQL/HOST/8080";
            Assert.Multiple(() =>
            {
                Assert.That(unscoped.ContainsKey("Datastore/all"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/allOther"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/MSSQL/all"), Is.True);
                Assert.That(unscoped.ContainsKey("Datastore/MSSQL/allOther"), Is.True);
                Assert.That(unscoped.ContainsKey(statementMetric), Is.True);
                Assert.That(unscoped.ContainsKey(operationMetric), Is.True);
                Assert.That(unscoped.ContainsKey(instanceMetric), Is.True);

                Assert.That(scoped.ContainsKey(statementMetric), Is.True);
            });

            var data = scoped[statementMetric];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            var unscopedMetricsWithExclusiveTime = new string[] { statementMetric, operationMetric, "Datastore/all", "Datastore/allOther", "Datastore/MSSQL/all", "Datastore/MSSQL/allOther" };

            foreach (var current in unscopedMetricsWithExclusiveTime)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
            }

            data = unscoped[instanceMetric];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });
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
