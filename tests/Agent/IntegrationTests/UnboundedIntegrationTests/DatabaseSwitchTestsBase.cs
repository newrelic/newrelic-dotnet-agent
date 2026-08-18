// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests;

/// <summary>
/// NR-576099: when an application changes the active database on an already-open
/// connection, each datastore segment must report the database its command actually
/// ran against, not the database named in the connection string.
///
/// Vendor-agnostic so the same harness covers every provider where the scenario is
/// possible. It drives an exerciser that runs one query against the connection-string
/// database, switches the active database on that same open connection, then runs a
/// second query against the switched-to database, and asserts the two resulting
/// segments carry different database_name values.
/// </summary>
public abstract class DatabaseSwitchTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;
    private readonly string _expectedTransactionName;
    private readonly string _switchedTableName;
    private readonly string _vendor;
    private readonly string _preSwitchTableName;
    private readonly string _connectionStringDatabaseName;
    private readonly string _switchedDatabaseName;

    /// <param name="fixture">Console fixture that hosts the exerciser application.</param>
    /// <param name="output">xunit output helper, wired to the fixture's test logger.</param>
    /// <param name="libraryNamespace">Namespace segment under MultiFunctionApplicationHelpers.NetStandardLibraries that holds the exerciser, e.g. "MsSql".</param>
    /// <param name="exerciserName">Exerciser class name, e.g. "MicrosoftDataSqlClientExerciser".</param>
    /// <param name="switchMethodName">Exerciser method that performs the switch, e.g. "MsSqlDatabaseSwitch".</param>
    /// <param name="vendor">Datastore vendor as it appears in segment names, e.g. "MSSQL".</param>
    /// <param name="preSwitchTableName">Table queried before the switch, lowercased as the agent reports it, e.g. "teammembers".</param>
    /// <param name="connectionStringDatabaseName">Database the segment before the switch must report.</param>
    /// <param name="switchedDatabaseName">Database created by the test and switched to. Fixed rather than generated so repeated runs against the long-lived shared containers reuse one database instead of accumulating new ones.</param>
    protected DatabaseSwitchTestsBase(
        TFixture fixture,
        ITestOutputHelper output,
        string libraryNamespace,
        string exerciserName,
        string switchMethodName,
        string vendor,
        string preSwitchTableName,
        string connectionStringDatabaseName,
        string switchedDatabaseName)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _vendor = vendor;
        _preSwitchTableName = preSwitchTableName;
        _connectionStringDatabaseName = connectionStringDatabaseName;
        _switchedDatabaseName = switchedDatabaseName;

        _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.{libraryNamespace}.{exerciserName}/{switchMethodName}";
        _switchedTableName = Utilities.GenerateTableName();

        _fixture.AddCommand($"{exerciserName} CreateDatabaseAndTable {_switchedDatabaseName} {_switchedTableName}");
        _fixture.AddCommand($"{exerciserName} {switchMethodName} {_switchedDatabaseName} {_switchedTableName}");
        _fixture.AddCommand($"{exerciserName} DropTableInDatabase {_switchedDatabaseName} {_switchedTableName}");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                configModifier.ForceTransactionTraces();
                configModifier.SetLogLevel("finest");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void EachSegmentReportsTheDatabaseItsCommandRanAgainst()
    {
        // The two segments must have DIFFERENT names, because
        // Assertions.TransactionTraceSegmentParametersExist keys on segmentName. That is why
        // the post-switch query targets a uniquely named table rather than re-querying the first.
        var preSwitchSegmentName = $"Datastore/statement/{_vendor}/{_preSwitchTableName}/select";
        var postSwitchSegmentName = $"Datastore/statement/{_vendor}/{_switchedTableName}/select";

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedTransactionName);

        Assert.NotNull(transactionSample);

        var expectedSegments = new List<string>
        {
            preSwitchSegmentName,
            postSwitchSegmentName
        };

        var expectedSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
        {
            new Assertions.ExpectedSegmentParameter { segmentName = preSwitchSegmentName, parameterName = "database_name", parameterValue = _connectionStringDatabaseName },
            new Assertions.ExpectedSegmentParameter { segmentName = postSwitchSegmentName, parameterName = "database_name", parameterValue = _switchedDatabaseName }
        };

        NrAssert.Multiple
        (
            () => Assertions.TransactionTraceSegmentsExist(expectedSegments, transactionSample),
            () => Assertions.TransactionTraceSegmentParametersExist(expectedSegmentParameters, transactionSample)
        );
    }
}
