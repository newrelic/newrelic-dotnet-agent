// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql;

// NR-576099 over MySQL. MySQL is the only database besides SQL Server where this scenario can
// occur: ChangeDatabase sends COM_INIT_DB on the live session, so the connection string does not
// change. PostgreSQL is deliberately not covered - Npgsql implements ChangeDatabase by closing,
// rewriting the connection string, and reopening, so the pre-existing per-connection-string cache
// already attributes it correctly and the live-database guard never fires.
//
// No wrapper code is MySQL-specific: MySql.Data, MySqlConnector, and Devart all route through the
// same SqlCommandWrapper that was fixed, which reads IDbConnection.Database generically.

/// <summary>
/// MySQL-specific values for the shared database-switch harness. The database name is lowercase
/// because MySQL database identifiers are case-sensitive on Linux, which is where the test
/// container runs.
/// </summary>
public abstract class MySqlDatabaseSwitchTestsBase<TFixture> : DatabaseSwitchTestsBase<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    protected MySqlDatabaseSwitchTestsBase(TFixture fixture, ITestOutputHelper output)
        : base(fixture, output,
            libraryNamespace: "MySql",
            exerciserName: "MySqlExerciser",
            switchMethodName: "MySqlDatabaseSwitch",
            vendor: "MySQL",
            preSwitchTableName: "dates",
            connectionStringDatabaseName: MySqlTestConfiguration.MySqlDbName,
            switchedDatabaseName: "nrdbswitchtest")
    {
    }
}

[Trait("Runtime", "Framework")]
public class MySqlDatabaseSwitchTests_FWLatest : MySqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MySqlDatabaseSwitchTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Runtime", "Core")]
public class MySqlDatabaseSwitchTests_CoreLatest : MySqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MySqlDatabaseSwitchTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
