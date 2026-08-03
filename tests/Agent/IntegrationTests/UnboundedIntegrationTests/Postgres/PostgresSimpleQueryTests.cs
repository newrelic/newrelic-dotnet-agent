// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.UnboundedIntegrationTests.Postgres;

public abstract class PostgresSqlSimpleQueryTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;

    public PostgresSqlSimpleQueryTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddCommand($"PostgresSqlExerciser SimpleQuery");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ConfigureFasterMetricsHarvestCycle(15);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                configModifier.ConfigureFasterSqlTracesHarvestCycle(15);

                configModifier.ForceTransactionTraces()
                    .SetLogLevel("finest");

                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
            },
            exerciseApplication: () =>
            {
                // Confirm transaction transform has completed before moving on to host application shutdown, and final sendDataOnExit harvest
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2)); // must be 2 minutes since this can take a while.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.PostgresSql.PostgresSqlExerciser/SimpleQuery";
        var expectedDatastoreCallCount = 1;

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Datastore/all", CallCountAllHarvests = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", CallCountAllHarvests = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/all", CallCountAllHarvests = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allOther", CallCountAllHarvests = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Npgsql.NpgsqlConnection/Open", CallCountAllHarvests = expectedDatastoreCallCount},
            new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Postgres/{CommonUtils.NormalizeHostname(PostgresConfiguration.PostgresServer)}/{PostgresConfiguration.PostgresPort}", CallCountAllHarvests = expectedDatastoreCallCount},
            new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", CallCountAllHarvests = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", CallCountAllHarvests = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", CallCountAllHarvests = expectedDatastoreCallCount, metricScope = expectedTransactionName},
        };
        var unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            // The datastore operation happened outside a web transaction so there should be no allWeb metrics
            new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb" },
            new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allWeb" },

            // The operation metric should not be scoped because the statement metric is scoped instead
            new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", CallCountAllHarvests = 1, metricScope = expectedTransactionName }
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

public class PostgresSqlSimpleQueryTestsFW462 : PostgresSqlSimpleQueryTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public PostgresSqlSimpleQueryTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class PostgresSqlSimpleQueryTestsFWLatest : PostgresSqlSimpleQueryTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public PostgresSqlSimpleQueryTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class PostgresSqlSimpleQueryTestsCoreOldest : PostgresSqlSimpleQueryTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public PostgresSqlSimpleQueryTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class PostgresSqlSimpleQueryTestsCoreLatest : PostgresSqlSimpleQueryTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public PostgresSqlSimpleQueryTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}