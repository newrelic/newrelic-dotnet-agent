// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql;

// NR-576099 over Microsoft.Data.SqlClient. The shared harness lives in
// DatabaseSwitchTestsBase; these classes only bind a fixture and a switch mechanism.

/// <summary>
/// MSSQL-specific values for the shared database-switch harness.
/// </summary>
public abstract class MsSqlDatabaseSwitchTestsBase<TFixture> : DatabaseSwitchTestsBase<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    protected MsSqlDatabaseSwitchTestsBase(TFixture fixture, ITestOutputHelper output, string exerciserName, string switchMethodName)
        : base(fixture, output,
            libraryNamespace: "MsSql",
            exerciserName: exerciserName,
            switchMethodName: switchMethodName,
            vendor: "MSSQL",
            preSwitchTableName: "teammembers",
            connectionStringDatabaseName: "NewRelic",
            switchedDatabaseName: "NrDbSwitchTest")
    {
    }
}

#region ChangeDatabase

public class MsSqlDatabaseSwitchTests_FWLatest : MsSqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlDatabaseSwitchTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser", "MsSqlDatabaseSwitch")
    {
    }
}

public class MsSqlDatabaseSwitchTests_CoreLatest : MsSqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MsSqlDatabaseSwitchTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser", "MsSqlDatabaseSwitch")
    {
    }
}

#endregion

#region USE statement

public class MsSqlDatabaseSwitchViaUseStatementTests_FWLatest : MsSqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlDatabaseSwitchViaUseStatementTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser", "MsSqlDatabaseSwitchViaUseStatement")
    {
    }
}

public class MsSqlDatabaseSwitchViaUseStatementTests_CoreLatest : MsSqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MsSqlDatabaseSwitchViaUseStatementTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser", "MsSqlDatabaseSwitchViaUseStatement")
    {
    }
}

#endregion

#region ChangeDatabaseAsync (Core only - the API does not exist on .NET Framework)

public class MsSqlDatabaseSwitchAsyncTests_CoreLatest : MsSqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MsSqlDatabaseSwitchAsyncTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "MicrosoftDataSqlClientExerciser", "MsSqlDatabaseSwitchAsync")
    {
    }
}

#endregion
