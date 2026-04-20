// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql;

public abstract class MySqlMetadataCommentTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;
    private const string TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MySql.MySqlExerciser/SingleDateQuery";

    public MySqlMetadataCommentTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddCommand("MySqlExerciser SingleDateQuery");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                configModifier.ConfigureFasterSqlTracesHarvestCycle(10);
                configModifier.ForceTransactionTraces().SetLogLevel("finest");

                configModifier.SetTransactionTracerExplainEnabled(true);
                configModifier.ForceSqlTraces();
                configModifier.SetTransactionTracerRecordSql("raw");
                configModifier.SetTransactionTracerSqlMetadataComments("nr_service,nr_service_guid,nr_txn,nr_trace_id");

                var instrumentationFilePath = string.Format(@"{0}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", fixture.DestinationNewRelicExtensionsDirectoryPath);
                CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
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

public class MySqlMetadataCommentTestsFW462 : MySqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public MySqlMetadataCommentTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class MySqlMetadataCommentTestsFWLatest : MySqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MySqlMetadataCommentTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class MySqlMetadataCommentTestsCoreOldest : MySqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MySqlMetadataCommentTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class MySqlMetadataCommentTestsCoreLatest : MySqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MySqlMetadataCommentTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
