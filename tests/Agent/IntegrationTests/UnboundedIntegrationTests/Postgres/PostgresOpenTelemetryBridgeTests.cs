// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.UnboundedIntegrationTests.Postgres;

// Native Npgsql instrumentation is capped at maxVersion="9" in the Sql wrapper. Npgsql 9.x+
// (which dropped the netstandard2.0 target and is .NET-only) is instead handled by the
// OpenTelemetry "hybrid agent" bridge, which consumes Npgsql's built-in "Npgsql"
// ActivitySource. "Npgsql" is on the bridge's default-excluded source list (so the native
// wrapper wins for < 9), so these tests must explicitly include the "Npgsql" source.
//
// The bridge produces only the core datastore set (Datastore/all[Other],
// Datastore/Postgres/all[Other], Datastore/operation, Datastore/statement); it does NOT
// reproduce native-only telemetry such as the DotNet/Npgsql.NpgsqlConnection/Open[Async]
// connection metrics, SQL traces, query parameters, the instance metric (Npgsql emits no
// server.port tag), or SQL metadata-comment injection. Assertions are limited accordingly.
public abstract class PostgresOTelBridgeTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;
    private readonly string _expectedTransactionName;

    protected PostgresOTelBridgeTestsBase(TFixture fixture, ITestOutputHelper output, string command) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.PostgresSql.PostgresSqlExerciser/{command}";

        _fixture.AddCommand($"PostgresSqlExerciser {command}");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ConfigureFasterMetricsHarvestCycle(15);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(15);

                configModifier.ForceTransactionTraces()
                    .SetLogLevel("finest");

                configModifier
                    .EnableOpenTelemetry(true)
                    .EnableOpenTelemetryTracing(true)
                    .IncludeActivitySource("Npgsql");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedDatastoreCallCount = 1;

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/all", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allOther", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = expectedDatastoreCallCount },
            new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = expectedDatastoreCallCount, metricScope = _expectedTransactionName },
        };
        var unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            // The datastore operation happened outside a web transaction so there should be no allWeb metrics
            new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb" },
            new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allWeb" },

            // The operation metric should not be scoped because the statement metric is scoped instead
            new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", metricScope = _expectedTransactionName },

            // These come only from the native connection-open wrapper, which is capped below v9.
            // Their absence confirms telemetry is flowing through the OTel bridge, not native instrumentation.
            new Assertions.ExpectedMetric { metricName = @"DotNet/Npgsql.NpgsqlConnection/Open" },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Npgsql.NpgsqlConnection/OpenAsync" },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

        var datastoreSpan = spanEvents.FirstOrDefault(@event =>
            @event.IntrinsicAttributes["name"].ToString() == "Datastore/statement/Postgres/teammembers/select");

        NrAssert.Multiple
        (
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
            () => Assert.NotNull(datastoreSpan)
        );
    }
}

public class PostgresOTelBridgeSimpleQueryTestsCoreLatest : PostgresOTelBridgeTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public PostgresOTelBridgeSimpleQueryTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "SimpleQuery")
    {
    }
}

public class PostgresOTelBridgeSimpleQueryAsyncTestsCoreLatest : PostgresOTelBridgeTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public PostgresOTelBridgeSimpleQueryAsyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "SimpleQueryAsync")
    {
    }
}
