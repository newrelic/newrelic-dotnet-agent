// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.UnboundedIntegrationTests.MySql;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.OtelDistro;

// MySqlConnector tests with the app running under the OTel distro (OTel native profiler + New Relic distro plugin)
// instead of the New Relic profiler. Same commands and assertions as the agent-mode MySqlConnector tests, including
// DotNet/DatabaseResult/Iterate: the base test sets enabled="true" on every tracerFactory in the on-disk Sql wrapper
// XML, which in distro mode turns on the shipped-disabled DataReaderTracer/DataReaderTracerAsync points.
// MySqlConnector versions: CoreOldest 1.0.1, FW48 2.1.2, FWLatest 2.6.2.
// This namespace is deliberately not in unbounded_all (.github/workflows/test_selection.yml): not run by CI
// until the distro is packaged. Requires NR_DEV_OTEL_HOME.

public class MySqlConnectorDistroTestsCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldestDistro>
{
    public MySqlConnectorDistroTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldestDistro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class MySqlConnectorDistroTestsFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48Distro>
{
    public MySqlConnectorDistroTestsFW48(ConsoleDynamicMethodFixtureFW48Distro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class MySqlConnectorDistroTestsFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatestDistro>
{
    public MySqlConnectorDistroTestsFWLatest(ConsoleDynamicMethodFixtureFWLatestDistro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
