// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
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
                configModifier.ConfigureFasterSpanEventsHarvestCycle(15);
                configModifier.ForceTransactionTraces();
                configModifier.SetLogLevel("finest");

                configModifier.ForceSqlTraces();
                configModifier.SetTransactionTracerExplainEnabled(true);
                configModifier.SetTransactionTracerRecordSql("raw");
                configModifier.SetTransactionTracerSqlMetadataCommentsEnabled(true);

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
        const string commentPrefix = "/*nr_service_guid=\"";

        // SQL traces
        var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
        var tracesForTransaction = sqlTraces.Where(t => t.TransactionName == TransactionName).ToList();

        Assert.True(tracesForTransaction.Count > 0, $"No SQL traces found for transaction {TransactionName}");

        foreach (var trace in tracesForTransaction)
        {
            Assert.True(
                trace.Sql.StartsWith(commentPrefix, StringComparison.Ordinal),
                $"Expected SQL trace to start with SQL metadata comment, but was: {trace.Sql}");
        }

        // Transaction trace segments
        var transactionSample = _fixture.AgentLog.TryGetTransactionSample(TransactionName);
        Assert.NotNull(transactionSample);

        var sqlSegments = GetAllSegments(transactionSample.TraceData.RootSegment)
            .Where(s => s.Parameters != null && s.Parameters.ContainsKey("sql"))
            .ToList();

        Assert.True(sqlSegments.Count > 0, "No SQL segments found in transaction trace");

        foreach (var segment in sqlSegments)
        {
            var sql = segment.Parameters["sql"] as string;
            Assert.True(
                sql?.StartsWith(commentPrefix, StringComparison.Ordinal) == true,
                $"Expected transaction trace segment SQL to start with metadata comment, but was: {sql}");
        }

        // Span events
        var dbSpans = _fixture.AgentLog.GetSpanEvents()
            .Where(s => s.AgentAttributes.ContainsKey("db.statement"))
            .ToList();

        Assert.True(dbSpans.Count > 0, "No span events with db.statement found");

        foreach (var span in dbSpans)
        {
            var sql = span.AgentAttributes["db.statement"] as string;
            Assert.True(
                sql?.StartsWith(commentPrefix, StringComparison.Ordinal) == true,
                $"Expected span event db.statement to start with metadata comment, but was: {sql}");
        }
    }

    private static IEnumerable<TransactionTraceSegment> GetAllSegments(TransactionTraceSegment segment)
    {
        yield return segment;
        if (segment.ChildSegments == null)
            yield break;
        foreach (var child in segment.ChildSegments)
            foreach (var descendant in GetAllSegments(child))
                yield return descendant;
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
