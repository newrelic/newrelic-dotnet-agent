// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
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
                configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                configModifier.ForceTransactionTraces().SetLogLevel("finest");

                configModifier.SetTransactionTracerExplainEnabled(true);
                configModifier.ForceSqlTraces();
                configModifier.SetTransactionTracerRecordSql("raw");
                configModifier.SetTransactionTracerSqlMetadataCommentsEnabled(true);

                var instrumentationFilePath = string.Format(@"{0}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", fixture.DestinationNewRelicExtensionsDirectoryPath);
                CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
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
