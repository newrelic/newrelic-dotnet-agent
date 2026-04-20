// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql;

public abstract class MsSqlMetadataCommentTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;
    private readonly string _expectedTransactionName;
    private readonly string _tableName;

    public MsSqlMetadataCommentTestsBase(TFixture fixture, ITestOutputHelper output, string exerciserName) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{exerciserName}/MsSql";
        _tableName = Utilities.GenerateTableName();

        _fixture.AddCommand($"{exerciserName} CreateTable {_tableName}");
        _fixture.AddCommand($"{exerciserName} MsSql {_tableName}");
        _fixture.AddCommand($"{exerciserName} DropTable {_tableName}");

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

                configModifier.SetTransactionTracerExplainEnabled(true);
                configModifier.ForceSqlTraces();
                configModifier.SetTransactionTracerRecordSql("raw");
                configModifier.SetTransactionTracerSqlMetadataComments("nr_service,nr_service_guid,nr_txn,nr_trace_id");

                var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
        var tracesForTransaction = sqlTraces.Where(t => t.TransactionName == _expectedTransactionName).ToList();

        Assert.True(tracesForTransaction.Count > 0, $"No SQL traces found for transaction {_expectedTransactionName}");

        foreach (var trace in tracesForTransaction)
        {
            Assert.True(
                trace.Sql.StartsWith("/*nr_service=\"", StringComparison.Ordinal),
                $"Expected SQL trace to start with SQL metadata comment, but was: {trace.Sql}");
        }
    }
}

#region System.Data.SqlClient

public class MsSqlMetadataCommentTests_SystemData_FWLatest : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlMetadataCommentTests_SystemData_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "SystemDataExerciser")
    {
    }
}

#endregion

#region Microsoft.Data.SqlClient

public class MsSqlMetadataCommentTests_MicrosoftDataSqlClient_FWLatest : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlMetadataCommentTests_MicrosoftDataSqlClient_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

public class MsSqlMetadataCommentTests_MicrosoftDataSqlClient_FW462 : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public MsSqlMetadataCommentTests_MicrosoftDataSqlClient_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

public class MsSqlMetadataCommentTests_MicrosoftDataSqlClient_CoreOldest : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MsSqlMetadataCommentTests_MicrosoftDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

public class MsSqlMetadataCommentTests_MicrosoftDataSqlClient_CoreLatest : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MsSqlMetadataCommentTests_MicrosoftDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

#endregion
