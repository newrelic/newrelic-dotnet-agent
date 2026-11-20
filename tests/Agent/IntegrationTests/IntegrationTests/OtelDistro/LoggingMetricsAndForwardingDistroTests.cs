// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.Logging;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Logging.MetricsAndForwarding;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OtelDistro;

// The logging-library metrics and forwarding tests (Logging.MetricsAndForwarding) in OTel distro mode: NR's own
// log4net, NLog, Serilog and Microsoft.Extensions.Logging instrumentation (XML + wrappers, compiled into the distro
// plugin by default) under the OTel native profiler + the New Relic distro plugin. Same assertions as the agent-mode
// classes, one .NET Framework and one .NET fixture per library. .NET uses CoreOldest (net10): OTel v1.18.0 has no net11
// (its start-up hook fails there, so CoreLatest gets no NR agent).
// This namespace is deliberately not in integration_all (.github/workflows/test_selection.yml): not run by CI until the
// distro is packaged. Requires NR_DEV_OTEL_HOME.

[Trait("Platform", "WindowsOnly")]
public class Log4NetMetricsAndForwardingDistroFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestDistro>
{
    public Log4NetMetricsAndForwardingDistroFWLatestTests(ConsoleDynamicMethodFixtureFWLatestDistro fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Log4net)
    {
    }
}

public class Log4NetMetricsAndForwardingDistroCoreOldestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreOldestDistro>
{
    public Log4NetMetricsAndForwardingDistroCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldestDistro fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Log4net)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class MELMetricsAndForwardingDistroFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestDistro>
{
    public MELMetricsAndForwardingDistroFWLatestTests(ConsoleDynamicMethodFixtureFWLatestDistro fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging)
    {
    }
}

public class MELMetricsAndForwardingDistroCoreOldestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreOldestDistro>
{
    public MELMetricsAndForwardingDistroCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldestDistro fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class SerilogMetricsAndForwardingDistroFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestDistro>
{
    public SerilogMetricsAndForwardingDistroFWLatestTests(ConsoleDynamicMethodFixtureFWLatestDistro fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog)
    {
    }
}

public class SerilogMetricsAndForwardingDistroCoreOldestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreOldestDistro>
{
    public SerilogMetricsAndForwardingDistroCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldestDistro fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class NLogMetricsAndForwardingDistroFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestDistro>
{
    public NLogMetricsAndForwardingDistroFWLatestTests(ConsoleDynamicMethodFixtureFWLatestDistro fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog)
    {
    }
}

public class NLogMetricsAndForwardingDistroCoreOldestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreOldestDistro>
{
    public NLogMetricsAndForwardingDistroCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldestDistro fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog)
    {
    }
}
