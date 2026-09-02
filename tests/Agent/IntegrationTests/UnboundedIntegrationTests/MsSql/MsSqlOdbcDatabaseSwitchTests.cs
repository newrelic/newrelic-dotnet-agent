// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql;

// NR-576099 over the ODBC driver. OdbcCommandWrapper is a separate wrapper from
// SqlCommandWrapper and needs its own coverage.

#region ChangeDatabase

[Trait("Runtime", "Framework")]
public class MsSqlOdbcDatabaseSwitchTests_FWLatest : MsSqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlOdbcDatabaseSwitchTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "SystemDataOdbcExerciser", "MsSqlDatabaseSwitch")
    {
    }
}

[Trait("Runtime", "Core")]
public class MsSqlOdbcDatabaseSwitchTests_CoreLatest : MsSqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MsSqlOdbcDatabaseSwitchTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "SystemDataOdbcExerciser", "MsSqlDatabaseSwitch")
    {
    }
}

#endregion

#region USE statement

[Trait("Runtime", "Framework")]
public class MsSqlOdbcDatabaseSwitchViaUseStatementTests_FWLatest : MsSqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MsSqlOdbcDatabaseSwitchViaUseStatementTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "SystemDataOdbcExerciser", "MsSqlDatabaseSwitchViaUseStatement")
    {
    }
}

[Trait("Runtime", "Core")]
public class MsSqlOdbcDatabaseSwitchViaUseStatementTests_CoreLatest : MsSqlDatabaseSwitchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MsSqlOdbcDatabaseSwitchViaUseStatementTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, "SystemDataOdbcExerciser", "MsSqlDatabaseSwitchViaUseStatement")
    {
    }
}

#endregion
