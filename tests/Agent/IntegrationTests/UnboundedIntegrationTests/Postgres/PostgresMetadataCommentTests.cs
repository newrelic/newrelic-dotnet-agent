// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.Postgres;

public abstract class PostgresMetadataCommentTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;
    private const string TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.PostgresSql.PostgresSqlExerciser/SimpleQuery";

    public PostgresMetadataCommentTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddCommand("PostgresSqlExerciser SimpleQuery");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ConfigureFasterMetricsHarvestCycle(15);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                configModifier.ConfigureFasterSqlTracesHarvestCycle(15);
                configModifier.ForceTransactionTraces().SetLogLevel("finest");

                configModifier.ForceSqlTraces();
                configModifier.SetTransactionTracerRecordSql("raw");
                configModifier.SetTransactionTracerSqlMetadataComments("nr_service,nr_service_guid,nr_txn,nr_trace_id");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
        var tracesForTransaction = sqlTraces.Where(t => t.TransactionName == TransactionName).ToList();

        Assert.True(tracesForTransaction.Count > 0, $"No SQL traces found for transaction {TransactionName}");

        foreach (var trace in tracesForTransaction)
        {
            Assert.True(
                trace.Sql.StartsWith("/*nr_service=\"", StringComparison.Ordinal),
                $"Expected SQL trace to start with SQL metadata comment, but was: {trace.Sql}");
        }
    }
}

public class PostgresMetadataCommentTestsFW462 : PostgresMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public PostgresMetadataCommentTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class PostgresMetadataCommentTestsFWLatest : PostgresMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public PostgresMetadataCommentTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class PostgresMetadataCommentTestsCoreOldest : PostgresMetadataCommentTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public PostgresMetadataCommentTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class PostgresMetadataCommentTestsCoreLatest : PostgresMetadataCommentTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public PostgresMetadataCommentTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
