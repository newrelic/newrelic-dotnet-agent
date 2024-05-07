// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared.Couchbase;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Couchbase
{
    [NetFrameworkTest]
    public class CouchbaseAsyncQueryRequestTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseAsyncQueryRequestTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();
                    configModifier.ForceSqlTraces();

                },
                exerciseApplication: () =>
                {
                    _fixture.Couchbase_QueryRequestAsync();

                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allWeb", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/QueryAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/QueryAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/QueryAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_QueryRequestAsync" },

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 1 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allOther"},

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/QueryAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_QueryRequestAsync" },
            };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/CouchbaseController/Couchbase_QueryRequestAsync",
                    Sql = "SELECT * FROM ? LIMIT ?",
                    DatastoreMetricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/QueryAsync"
                }
            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };

            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/QueryAsync", parameterName = "sql", parameterValue = "SELECT * FROM ? LIMIT ?"}
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/CouchbaseController/Couchbase_QueryRequestAsync");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_QueryRequestAsync");

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample)
            );
        }
    }
}
