// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using System.Collections.Generic;
using System.Data;

namespace CompositeTests
{
    [TestFixture]
    public class SqlParsingCacheSupportabilityMetricTests
    {
        private const string SqlParsingCacheMetricPrefix = "Supportability/Cache/SqlParsingCache";
        private const string SqlObfuscationMetricPrefix = "Supportability/Cache/SqlObfuscationCache";

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
        public void SqlParsingCacheMetricsAreGenerated()
        {
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table2").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table2").End();

            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.IBMDB2, CommandType.Text, "SELECT * FROM Table1").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.IBMDB2, CommandType.Text, "SELECT * FROM Table1").End();

            transaction.End();

            _compositeTestAgent.Harvest();

            const int defaultCapacity = 1000;

            // ASSERT
            var expectedMetrics = new List<ExpectedMetric>
            {
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Capacity", CallCount = 1, Total = defaultCapacity},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Hits", CallCount = 3},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Misses", CallCount = 2},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Ejections", CallCount = 0},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Size", CallCount = 1, Total = 2},

                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Capacity", CallCount = 1, Total = defaultCapacity},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Hits", CallCount = 1},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Misses", CallCount = 1},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Ejections", CallCount = 0},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Size", CallCount = 1, Total = 1},

                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/MSSQL/Capacity", CallCount = 1, Total = defaultCapacity},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/MSSQL/Hits", CallCount = 3},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/MSSQL/Misses", CallCount = 2},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/MSSQL/Ejections", CallCount = 0},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/MSSQL/Size", CallCount = 1, Total = 2},

                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/IBMDB2/Capacity", CallCount = 1, Total = defaultCapacity},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/IBMDB2/Hits", CallCount = 1},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/IBMDB2/Misses", CallCount = 1},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/IBMDB2/Ejections", CallCount = 0},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/IBMDB2/Size", CallCount = 1, Total = 1}
            };

            var unexpectedMetrics = new List<ExpectedMetric>
            {
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MongoDB/Hits"},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MongoDB/Misses"},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MongoDB/Ejections"},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MongoDB/Size"},

                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/MongoDB/Hits"},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/MongoDB/Misses"},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/MongoDB/Ejections"},
                new ExpectedCountMetric {Name =  SqlObfuscationMetricPrefix + "/MongoDB/Size"}
            };

            MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
            MetricAssertions.MetricsDoNotExist(unexpectedMetrics, _compositeTestAgent.Metrics);
        }

        [Test]
        public void SqlParsingCacheMetricsAreResetBetweenHarvests()
        {
            _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table2").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table2").End();

            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.IBMDB2, CommandType.Text, "SELECT * FROM Table1").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.IBMDB2, CommandType.Text, "SELECT * FROM Table1").End();

            const int defaultCapacity = 1000;

            // ASSERT
            var expectedMetrics = new List<ExpectedMetric>
            {
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Capacity", CallCount = 1, Total = defaultCapacity},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Hits", CallCount = 3},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Misses", CallCount = 2},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Ejections", CallCount = 0},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Size", CallCount = 1, Total = 2},

                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Capacity", CallCount = 1, Total = defaultCapacity},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Hits", CallCount = 1},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Misses", CallCount = 1},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Ejections", CallCount = 0},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/IBMDB2/Size", CallCount = 1, Total = 1}
            };

            _compositeTestAgent.Harvest();

            MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);

            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
            _agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();

            expectedMetrics = new List<ExpectedMetric>
            {
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Capacity", CallCount = 1, Total = defaultCapacity},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Hits", CallCount = 2},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Misses", CallCount = 0},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Ejections", CallCount = 0},
                new ExpectedCountMetric {Name =  SqlParsingCacheMetricPrefix + "/MSSQL/Size", CallCount = 1, Total = 2}
            };

            _compositeTestAgent.ResetHarvestData();
            _compositeTestAgent.Harvest();

            MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
        }
    }
}
