// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.UnboundedIntegrationTests.Postgres;

public abstract class PostgresSqlIteratorAsyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;

    // Iterate metrics roll up from NpgsqlDataReader Read/NextResult calls. Through Npgsql 7 the async read
    // loop over a single-row result yields 3 (read row, internal NextResult, final read returning false).
    // Npgsql 8 refactored the async path so the result-set teardown no longer surfaces through an instrumented
    // method, yielding 2; fixtures that pin Npgsql 8.x or later override this.
    protected virtual int ExpectedIterationCount => 3;

    public PostgresSqlIteratorAsyncTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddCommand($"PostgresSqlExerciser AsyncIteratorTest");

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

                var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "DataReaderTracerAsync", "enabled", "true");
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
        var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.PostgresSql.PostgresSqlExerciser/AsyncIteratorTest";
        var expectedDatastoreCallCount = 1;

        // Number of Iterate rollups expected for this read loop; varies by Npgsql version (see ExpectedIterationCount).
        var expectedIterationCount = ExpectedIterationCount;

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/all", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allOther", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Postgres/{CommonUtils.NormalizeHostname(PostgresConfiguration.PostgresServer)}/{PostgresConfiguration.PostgresPort}", callCount = expectedDatastoreCallCount},
            new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1, metricScope = expectedTransactionName},

            // NpgsqlDataReader methods Read/ReadAsync and NextResult/NextResultAsync result in Iterate metrics.
            new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterationCount },
            new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterationCount, metricScope = expectedTransactionName}
        };
        var unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb" },
            new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allWeb" },

            // The operation metric should not be scoped because the statement metric is scoped instead
            new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", metricScope = expectedTransactionName }
        };
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        NrAssert.Multiple
        (
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)
        );
    }
}

public class PostgresSqlIteratorAsyncTestsFW462 : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public PostgresSqlIteratorAsyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class PostgresSqlIteratorAsyncTestsFWLatest : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    // Npgsql 8.x async read path yields 2 Iterate rollups instead of 3 (see base class).
    protected override int ExpectedIterationCount => 2;

    public PostgresSqlIteratorAsyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class PostgresSqlIteratorAsyncTestsCoreOldest : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    // Npgsql 8.x async read path yields 2 Iterate rollups instead of 3 (see base class).
    protected override int ExpectedIterationCount => 2;

    public PostgresSqlIteratorAsyncTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}

public class PostgresSqlIteratorAsyncTestsCoreLatest : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    // Npgsql 8.x async read path yields 2 Iterate rollups instead of 3 (see base class).
    protected override int ExpectedIterationCount => 2;

    public PostgresSqlIteratorAsyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {

    }
}