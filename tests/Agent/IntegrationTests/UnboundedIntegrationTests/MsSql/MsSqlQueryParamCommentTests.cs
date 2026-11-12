// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql;

// Verifies that a SQL comment doesn't break parameter inlining or explain plan generation.
public class MsSqlQueryParamCommentTests : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureCoreLatest>
{
    private const string TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.MicrosoftDataSqlClientExerciser/MsSqlWithParameterizedQueryAndComment";
    private readonly ConsoleDynamicMethodFixtureCoreLatest _fixture;

    public MsSqlQueryParamCommentTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddCommand("MicrosoftDataSqlClientExerciser MsSqlWithParameterizedQueryAndComment True");

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
                configModifier.SetLogLevel("finest");       //This has to stay at finest to ensure parameter check security

                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");

                var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "NewRelic.Providers.Wrapper.Sql.Instrumentation.xml");
                CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForMetricAggregateCallCount("Datastore/all", 1, TimeSpan.FromMinutes(2));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedQueryParameters = new Dictionary<string, object> { { "@FN", "O'Keefe" } };

        var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
        {
            new Assertions.ExpectedSqlTrace
            {
                TransactionName = TransactionName,
                Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN -- just a plain comment",
                DatastoreMetricName = "Datastore/statement/MSSQL/teammembers/select",
                QueryParameters = expectedQueryParameters,
                HasExplainPlan = true
            }
        };

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample(TransactionName);
        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(TransactionName);
        var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
        var logEntries = _fixture.AgentLog.GetFileLines();

        var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
        {
            new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "explain_plan" },
            new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "sql", parameterValue = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN -- just a plain comment" },
        };

        NrAssert.Multiple(
            () => Assert.NotNull(transactionSample),
            () => Assert.NotNull(transactionEvent)
        );

        NrAssert.Multiple
        (
            () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample),
            () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
            () => Assertions.LogLinesNotExist(new[] { AgentLogFile.ErrorLogLinePrefixRegex }, logEntries)
        );

        // HasExplainPlan above only checks that the "explain_plan" key is present and non-null -- an agent bug
        // that generates a present-but-empty plan (e.g. zero rows) would slip past that check. Walk the plan
        // content itself to prove the comment was stripped before placeholder matching and the query still
        // ran and explained correctly.
        var stmtTexts = GetExplainPlanStmtTexts(sqlTraces);
        Assert.True(stmtTexts.Any(IsShowPlanOperatorRow), "No explain plan StmtText row kept its operator text after the |-- prefix: " + string.Join(" | ", stmtTexts));
    }

    private static List<string> GetExplainPlanStmtTexts(IEnumerable<SqlTrace> sqlTraces)
    {
        var stmtTexts = new List<string>();
        foreach (var sqlTrace in sqlTraces)
        {
            if (!sqlTrace.ParameterData.TryGetValue("explain_plan", out var value) || value is not JArray explainPlan)
            {
                continue;
            }

            var stmtTextIndex = explainPlan[0].ToObject<List<string>>().IndexOf("StmtText");
            foreach (var row in explainPlan[1])
            {
                stmtTexts.Add(row[stmtTextIndex]?.ToString());
            }
        }

        return stmtTexts;
    }

    // "  |--Clustered Index Scan(...)" passes; "  |?" (the prefix read as a -- comment) does not.
    private static bool IsShowPlanOperatorRow(string stmtText)
    {
        var prefixIndex = stmtText?.IndexOf("|--", StringComparison.Ordinal) ?? -1;
        return prefixIndex >= 0 && prefixIndex + 3 < stmtText.Length && char.IsLetter(stmtText[prefixIndex + 3]);
    }
}
