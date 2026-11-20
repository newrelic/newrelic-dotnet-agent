// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.UnboundedIntegrationTests.MySql;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.OtelDistro;

// MySql async tests with the app running under the OTel distro (OTel native profiler + New Relic distro plugin)
// instead of the New Relic profiler. Same commands and assertions as the agent-mode MySql async tests.
// The three classes cover the three MySql.Data version bands of MySqlConnection.OpenAsync in the Sql wrapper XML:
// CoreOldest 8.0.28 (below 8.0.33), FW48 8.4.0 (8.0.33 up to 9.7.0), FWLatest 26.7.0 (9.7.0 and up).
// This namespace is deliberately not in unbounded_all (.github/workflows/test_selection.yml): not run by CI
// until the distro is packaged. Requires NR_DEV_OTEL_HOME.

public class MySqlAsyncDistroTestsCoreOldest : MySqlAsyncTestsBase<ConsoleDynamicMethodFixtureCoreOldestDistro>
{
    public MySqlAsyncDistroTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldestDistro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class MySqlAsyncDistroTestsFW48 : MySqlAsyncTestsBase<ConsoleDynamicMethodFixtureFW48Distro>
{
    public MySqlAsyncDistroTestsFW48(ConsoleDynamicMethodFixtureFW48Distro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class MySqlAsyncDistroTestsFWLatest : MySqlAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatestDistro>
{
    public MySqlAsyncDistroTestsFWLatest(ConsoleDynamicMethodFixtureFWLatestDistro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
