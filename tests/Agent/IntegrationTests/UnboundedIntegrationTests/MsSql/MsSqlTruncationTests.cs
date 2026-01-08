// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;


namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public abstract class MsSqlTruncationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        // SQL statement maximum length is 4096 bytes, including 3-character ellipsis ("...") if truncation occurs
        private const int MaxSqlLength = 4096;
        private const string Ellipsis = "...";

        public MsSqlTruncationTestsBase(TFixture fixture, ITestOutputHelper output, string exerciserName) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.BaselinePayloadBytes = 61446; // Baseline payload size for Core Latest agent with Microsoft.Data.SqlClient

            _fixture.AddCommand($"{exerciserName} MsSqlWithLongQuery");
            _fixture.AddCommand($"{exerciserName} Wait 5000");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(15);

                    configModifier.SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            // find the Datastore/statement/MSSQL/teammembers/select span
            var sqlSpan = _fixture.AgentLog.TryGetSpanEvent("Datastore/statement/MSSQL/teammembers/select");
            Assert.NotNull(sqlSpan);

            // get the db.statement attribute
            Assert.True(sqlSpan.AgentAttributes.ContainsKey("db.statement"), "Span should have 'db.statement' attribute");
            var sqlStatement = sqlSpan.AgentAttributes["db.statement"] as string;

            Assert.NotNull(sqlStatement);

            // Verify the SQL statement is truncated to exactly 4096 bytes
            var sqlStatementBytes = Encoding.UTF8.GetBytes(sqlStatement);
            Assert.Equal(MaxSqlLength, sqlStatementBytes.Length);

            // Verify the SQL statement ends with the ellipsis
            Assert.EndsWith(Ellipsis, sqlStatement);
        }
    }

    #region System.Data.SqlClient

    public class MsSqlTruncationTests_MicrosoftDataSqlClient_CoreLatest : MsSqlTruncationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlTruncationTests_MicrosoftDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  exerciserName: "MicrosoftDataSqlClientExerciser")
        {
        }
    }

    #endregion
}
