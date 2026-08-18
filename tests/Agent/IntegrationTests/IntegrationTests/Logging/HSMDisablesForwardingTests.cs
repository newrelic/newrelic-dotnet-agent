// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Logging.Hsm;

public abstract class HSMDisablesForwardingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;

    public HSMDisablesForwardingTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        _fixture.AddCommand($"LoggingTester SetFramework {loggingFramework} {RandomPortGenerator.NextPort()}");
        _fixture.AddCommand($"LoggingTester Configure");
        _fixture.AddCommand($"LoggingTester CreateSingleLogMessage One DEBUG");
        _fixture.AddCommand($"LoggingTester CreateSingleLogMessage Two INFO");
        _fixture.AddCommand($"LoggingTester CreateSingleLogMessage Three WARN");
        _fixture.AddCommand($"LoggingTester CreateSingleLogMessage Four ERROR");
        _fixture.AddCommand($"LoggingTester CreateSingleLogMessage GetYourLogsOnTheDanceFloor FATAL");

        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                // applicationLogging metrics and forwarding enabled by default
                configModifier
                    .SetLogLevel("debug");
                configModifier.DisableEventListenerSamplers(); // Required for .NET 10+ to pass.

                // Set HSM to "true"
                configModifier.SetHighSecurityMode(true);
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void NoLogDataIsSent()
    {
        var logData = _fixture.AgentLog.GetLogEventData().FirstOrDefault();
        Assert.Null(logData);

        // Making sure logging metrics aren't disabled
        var loggingMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = "Logging/lines", CallCountAllHarvests = 5 },
        };

        var actualMetrics = _fixture.AgentLog.GetMetrics();
        Assertions.MetricsExist(loggingMetrics, actualMetrics);

    }
}

#region log4net

public class Log4netHSMDisablesForwardingTestsFWLatestTests : HSMDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
{
    public Log4netHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Log4net)
    {
    }
}

public class Log4netHSMDisablesForwardingTestsNetCoreLatestTests : HSMDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
{
    public Log4netHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Log4net)
    {
    }
}

#endregion

#region MicrosoftLogging

public class MicrosoftLoggingHSMDisablesForwardingTestsNetCoreLatestTests : HSMDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
{
    public MicrosoftLoggingHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging)
    {
    }
}

#endregion

#region Serilog

public class SerilogHSMDisablesForwardingTestsFWLatestTests : HSMDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
{
    public SerilogHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog)
    {
    }
}

public class SerilogHSMDisablesForwardingTestsNetCoreLatestTests : HSMDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
{
    public SerilogHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog)
    {
    }
}

#endregion

#region NLog

public class NLogHSMDisablesForwardingTestsFWLatestTests : HSMDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
{
    public NLogHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog)
    {
    }
}

public class NLogHSMDisablesForwardingTestsNetCoreLatestTests : HSMDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
{
    public NLogHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog)
    {
    }
}

#endregion

#region Sitecore

public class SitecoreHSMDisablesForwardingTestsFWLatestTests : HSMDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
{
    public SitecoreHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Sitecore)
    {
    }
}

#endregion
