// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.Oracle;

public abstract class OracleMetadataCommentTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;
    private const string TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync";

    public OracleMetadataCommentTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        var tableName = GenerateTableName();
        _fixture.AddCommand($"OracleExerciser InitializeTable {tableName}");
        _fixture.AddCommand("OracleExerciser ExerciseSync");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ConfigureFasterMetricsHarvestCycle(15);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                configModifier.ConfigureFasterSqlTracesHarvestCycle(15);
                configModifier.ForceTransactionTraces();
                configModifier.SetLogLevel("finest");

                configModifier.ForceSqlTraces();
                configModifier.SetTransactionTracerExplainEnabled(true);
                configModifier.SetTransactionTracerRecordSql("raw");
                configModifier.SetTransactionTracerSqlMetadataComments("nr_service,nr_service_guid,nr_txn,nr_trace_id");

                var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromMinutes(1));
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

    private static string GenerateTableName()
    {
        var tableId = Guid.NewGuid().ToString("N").Substring(2, 29).ToLower();
        return $"h{tableId}";
    }
}

public class OracleMetadataCommentTestsFramework462 : OracleMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public OracleMetadataCommentTestsFramework462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class OracleMetadataCommentTestsFrameworkLatest : OracleMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public OracleMetadataCommentTestsFrameworkLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class OracleMetadataCommentTestsCoreLatest : OracleMetadataCommentTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public OracleMetadataCommentTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
