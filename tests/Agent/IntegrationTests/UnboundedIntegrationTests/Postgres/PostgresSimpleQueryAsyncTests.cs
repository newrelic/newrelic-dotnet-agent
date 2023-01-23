// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Postgres
{
    public abstract class PostgresSqlSimpleQueryAsyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        public PostgresSqlSimpleQueryAsyncTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"PostgresSqlExerciser SimpleQueryAsync");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces()
                    .SetLogLevel("finest");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                }
            );

            // Confirm transaction transform has completed before moving on to host application shutdown, and final sendDataOnExit harvest
            _fixture.AddActions(exerciseApplication: () => _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2)));

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.PostgresSql.PostgresSqlExerciser/SimpleQueryAsync";
            var expectedDatastoreCallCount = 1;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allOther", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Npgsql.NpgsqlConnection/Open", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Postgres/{CommonUtils.NormalizeHostname(PostgresConfiguration.PostgresServer)}/{PostgresConfiguration.PostgresPort}", callCount = expectedDatastoreCallCount},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1, metricScope = expectedTransactionName},
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// The datastore operation happened outside a web transaction so there should be no allWeb metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allWeb" },

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", callCount = 1, metricScope = expectedTransactionName }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/Postgres/teammembers/select"
            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = expectedTransactionName,
                    Sql = "SELECT * FROM newrelic.teammembers WHERE firstname = ?",
                    DatastoreMetricName = "Datastore/statement/Postgres/teammembers/select",
                    HasExplainPlan = false
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(expectedTransactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),

                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );
        }
    }

    [NetFrameworkTest]
    public class PostgresSqlSimpleQueryAsyncTestsFW462 : PostgresSqlSimpleQueryAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public PostgresSqlSimpleQueryAsyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class PostgresSqlSimpleQueryAsyncTestsFW471 : PostgresSqlSimpleQueryAsyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public PostgresSqlSimpleQueryAsyncTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class PostgresSqlSimpleQueryAsyncTestsFW48 : PostgresSqlSimpleQueryAsyncTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public PostgresSqlSimpleQueryAsyncTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class PostgresSqlSimpleQueryAsyncTestsFWLatest : PostgresSqlSimpleQueryAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public PostgresSqlSimpleQueryAsyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlSimpleQueryAsyncTestsCore31 : PostgresSqlSimpleQueryAsyncTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public PostgresSqlSimpleQueryAsyncTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlSimpleQueryAsyncTestsCore50 : PostgresSqlSimpleQueryAsyncTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public PostgresSqlSimpleQueryAsyncTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlSimpleQueryAsyncTestsCore60 : PostgresSqlSimpleQueryAsyncTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public PostgresSqlSimpleQueryAsyncTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlSimpleQueryAsyncTestsCoreLatest : PostgresSqlSimpleQueryAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public PostgresSqlSimpleQueryAsyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }
}
