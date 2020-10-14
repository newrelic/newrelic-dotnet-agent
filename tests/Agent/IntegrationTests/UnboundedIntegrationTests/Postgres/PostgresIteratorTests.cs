// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Postgres
{
    [NetFrameworkTest]
    public class PostgresIteratorTests : IClassFixture<PostgresBasicMvcFixture>
    {
        private readonly PostgresBasicMvcFixture _fixture;

        public PostgresIteratorTests(PostgresBasicMvcFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "DataReaderTracer", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.PostgresIteratorTest();
                    _fixture.PostgresAsyncIteratorTest();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedSyncTransactionName = "WebTransaction/MVC/PostgresController/PostgresIteratorTest";
            var expectedAsyncTransactionName = "WebTransaction/MVC/PostgresController/PostgresAsyncIteratorTest";
            var expectedDatastoreCallCount = 2;

            //These values are dictated by the queries that are being run as part of this test.
            //There are two application endpoints being exercised by the test, each of which runs a query that returns a single row.
            //The typical pattern in this case is for there to be a call to Read(), followed by a call to NextResult(), followed by a final call to
            //Read() which returns false to exit the loop.  Each of these roll up to Iterate for a total of 3 for each endpoint.
            var expectedSyncIterationCount = 3;
            var expectedAsyncIterationCount = 3;
            var expectedIterationCount = expectedSyncIterationCount + expectedAsyncIterationCount;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allWeb", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Postgres/{CommonUtils.NormalizeHostname(PostgresConfiguration.PostgresServer)}/{PostgresConfiguration.PostgresPort}", callCount = expectedDatastoreCallCount},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1, metricScope = expectedSyncTransactionName},
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1, metricScope = expectedAsyncTransactionName},

                // NpgsqlDataReader methods Read/ReadAsync and NextResult/NextResultAsync result in Iterate metrics.
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterationCount },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedSyncIterationCount, metricScope = expectedSyncTransactionName},
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedAsyncIterationCount, metricScope = expectedAsyncTransactionName}
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a web transaction so there should be no allOther metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allOther" },

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", metricScope = expectedSyncTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", metricScope = expectedAsyncTransactionName }
            };
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)
            );
        }
    }
}
