// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
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
                configModifier.ConfigureFasterSpanEventsHarvestCycle(15);
                configModifier.ForceTransactionTraces();
                configModifier.SetLogLevel("finest");

                configModifier.SetTransactionTracerExplainEnabled(true);
                configModifier.ForceSqlTraces();
                configModifier.SetTransactionTracerRecordSql("raw");
                configModifier.SetTransactionTracerSqlMetadataCommentsEnabled(true);

                var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
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
        var tracesForTransaction = sqlTraces.Where(t => t.TransactionName == _expectedTransactionName).ToList();

        Assert.True(tracesForTransaction.Count > 0, $"No SQL traces found for transaction {_expectedTransactionName}");

        foreach (var trace in tracesForTransaction)
        {
            Assert.True(
                trace.Sql.StartsWith(commentPrefix, StringComparison.Ordinal),
                $"Expected SQL trace to start with SQL metadata comment, but was: {trace.Sql}");
        }

        // Transaction trace segments
        var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedTransactionName);
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

public abstract class MsSqlMetadataCommentTestsStoredProcBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;
    private readonly string _expectedTransactionName;
    private readonly string _procedureNameWith;
    private readonly string _procedureNameWithout;

    public MsSqlMetadataCommentTestsStoredProcBase(TFixture fixture, ITestOutputHelper output, string exerciserName) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{exerciserName}/MsSqlParameterizedStoredProcedure";
        _procedureNameWith = Utilities.GenerateProcedureName();
        _procedureNameWithout = Utilities.GenerateProcedureName();

        _fixture.AddCommand($"{exerciserName} MsSqlParameterizedStoredProcedure {_procedureNameWith} {_procedureNameWithout}");
        _fixture.AddCommand($"{exerciserName} DropProcedure {_procedureNameWith}");
        _fixture.AddCommand($"{exerciserName} DropProcedure {_procedureNameWithout}");

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

                configModifier.SetTransactionTracerExplainEnabled(true);
                configModifier.ForceSqlTraces();
                configModifier.SetTransactionTracerRecordSql("raw");
                configModifier.SetTransactionTracerSqlMetadataCommentsEnabled(true);

                var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        const string commentPrefix = "/*nr_service_guid=\"";

        // MsSqlParameterizedStoredProcedure also runs a CREATE OR ALTER PROCEDURE statement via
        // EnsureProcedure (CommandType.Text) to set up the procedure before calling it. That setup
        // statement is expected to still carry the comment -- it's not the CommandType.StoredProcedure
        // call under test here, so it must be excluded from the "no comment" assertions below.
        bool IsSetupStatement(string sql) => sql != null && sql.Contains("CREATE OR ALTER PROCEDURE", StringComparison.OrdinalIgnoreCase);

        // SQL traces
        var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
        var tracesForTransaction = sqlTraces
            .Where(t => t.TransactionName == _expectedTransactionName)
            .Where(t => !IsSetupStatement(t.Sql))
            .ToList();

        Assert.True(tracesForTransaction.Count > 0, $"No stored procedure SQL traces found for transaction {_expectedTransactionName}");

        foreach (var trace in tracesForTransaction)
        {
            Assert.False(
                trace.Sql.StartsWith(commentPrefix, StringComparison.Ordinal),
                $"Expected SQL trace for a stored procedure call to NOT have the SQL metadata comment, but was: {trace.Sql}");
        }

        // Transaction trace segments
        var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedTransactionName);
        Assert.NotNull(transactionSample);

        var sqlSegments = GetAllSegments(transactionSample.TraceData.RootSegment)
            .Where(s => s.Parameters != null && s.Parameters.ContainsKey("sql"))
            .Where(s => !IsSetupStatement(s.Parameters["sql"] as string))
            .ToList();

        Assert.True(sqlSegments.Count > 0, "No stored procedure SQL segments found in transaction trace");

        foreach (var segment in sqlSegments)
        {
            var sql = segment.Parameters["sql"] as string;
            Assert.False(
                sql?.StartsWith(commentPrefix, StringComparison.Ordinal) == true,
                $"Expected transaction trace segment SQL for a stored procedure call to NOT have the SQL metadata comment, but was: {sql}");
        }

        // Span events
        var dbSpans = _fixture.AgentLog.GetSpanEvents()
            .Where(s => s.AgentAttributes.ContainsKey("db.statement"))
            .Where(s => !IsSetupStatement(s.AgentAttributes["db.statement"] as string))
            .ToList();

        Assert.True(dbSpans.Count > 0, "No stored procedure span events with db.statement found");

        foreach (var span in dbSpans)
        {
            var sql = span.AgentAttributes["db.statement"] as string;
            Assert.False(
                sql?.StartsWith(commentPrefix, StringComparison.Ordinal) == true,
                $"Expected span event db.statement for a stored procedure call to NOT have the SQL metadata comment, but was: {sql}");
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

#region System.Data.SqlClient

[Trait("Runtime", "Framework")]
public class MsSqlMetadataCommentTests_SystemData_FWLatest : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlMetadataCommentTests_SystemData_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "SystemDataExerciser")
    {
    }
}

[Trait("Runtime", "Framework")]
public class MsSqlMetadataCommentTestsStoredProc_SystemData_FWLatest : MsSqlMetadataCommentTestsStoredProcBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlMetadataCommentTestsStoredProc_SystemData_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "SystemDataExerciser")
    {
    }
}

#endregion

#region Microsoft.Data.SqlClient

[Trait("Runtime", "Framework")]
public class MsSqlMetadataCommentTests_MicrosoftDataSqlClient_FWLatest : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlMetadataCommentTests_MicrosoftDataSqlClient_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

[Trait("Runtime", "Framework")]
public class MsSqlMetadataCommentTests_MicrosoftDataSqlClient_FW462 : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public MsSqlMetadataCommentTests_MicrosoftDataSqlClient_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

[Trait("Runtime", "Core")]
public class MsSqlMetadataCommentTests_MicrosoftDataSqlClient_CoreOldest : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MsSqlMetadataCommentTests_MicrosoftDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

[Trait("Runtime", "Core")]
public class MsSqlMetadataCommentTests_MicrosoftDataSqlClient_CoreLatest : MsSqlMetadataCommentTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MsSqlMetadataCommentTests_MicrosoftDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

[Trait("Runtime", "Framework")]
public class MsSqlMetadataCommentTestsStoredProc_MicrosoftDataSqlClient_FWLatest : MsSqlMetadataCommentTestsStoredProcBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlMetadataCommentTestsStoredProc_MicrosoftDataSqlClient_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

[Trait("Runtime", "Framework")]
public class MsSqlMetadataCommentTestsStoredProc_MicrosoftDataSqlClient_FW462 : MsSqlMetadataCommentTestsStoredProcBase<ConsoleDynamicMethodFixtureFW462>
{
    public MsSqlMetadataCommentTestsStoredProc_MicrosoftDataSqlClient_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

[Trait("Runtime", "Core")]
public class MsSqlMetadataCommentTestsStoredProc_MicrosoftDataSqlClient_CoreOldest : MsSqlMetadataCommentTestsStoredProcBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MsSqlMetadataCommentTestsStoredProc_MicrosoftDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

[Trait("Runtime", "Core")]
public class MsSqlMetadataCommentTestsStoredProc_MicrosoftDataSqlClient_CoreLatest : MsSqlMetadataCommentTestsStoredProcBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MsSqlMetadataCommentTestsStoredProc_MicrosoftDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser")
    {
    }
}

#endregion
