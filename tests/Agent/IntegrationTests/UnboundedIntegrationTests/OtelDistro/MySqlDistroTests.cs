// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.UnboundedIntegrationTests.MySql;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.OtelDistro;

// MySql (MySql.Data) tests with the app running under the OTel distro (OTel native profiler + New Relic distro plugin)
// instead of the New Relic profiler. Same commands and assertions as the agent-mode MySql tests, including
// DotNet/DatabaseResult/Iterate: the base test sets enabled="true" on every tracerFactory in the on-disk Sql wrapper
// XML, which in distro mode turns on the shipped-disabled DataReaderTracer points.
// MySql.Data versions: CoreOldest 8.0.28, FW48 8.4.0, FWLatest 26.7.0.
// This namespace is deliberately not in unbounded_all (.github/workflows/test_selection.yml): not run by CI
// until the distro is packaged. Requires NR_DEV_OTEL_HOME.

public class MySqlDistroTestsCoreOldest : MySqlTestsBase<ConsoleDynamicMethodFixtureCoreOldestDistro>
{
    public MySqlDistroTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldestDistro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class MySqlDistroTestsFW48 : MySqlTestsBase<ConsoleDynamicMethodFixtureFW48Distro>
{
    public MySqlDistroTestsFW48(ConsoleDynamicMethodFixtureFW48Distro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class MySqlDistroTestsFWLatest : MySqlTestsBase<ConsoleDynamicMethodFixtureFWLatestDistro>
{
    public MySqlDistroTestsFWLatest(ConsoleDynamicMethodFixtureFWLatestDistro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
