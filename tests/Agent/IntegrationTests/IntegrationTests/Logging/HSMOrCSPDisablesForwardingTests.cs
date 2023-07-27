// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.HsmAndCsp
{
    public abstract class HSMOrCSPDisablesForwardingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public HSMOrCSPDisablesForwardingTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {loggingFramework}");
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

                    if (typeof(TFixture).ToString().Contains("HSM"))
                    {
                        // Set HSM to "true"
                        configModifier.SetHighSecurityMode(true);
                    }
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
                new Assertions.ExpectedMetric { metricName = "Logging/lines", callCount = 5 },
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(loggingMetrics, actualMetrics);

        }
    }

    #region log4net

    [NetFrameworkTest]
    public class Log4netHSMDisablesForwardingTestsFWLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
    {
        public Log4netHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netCSPDisablesForwardingTestsFWLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestCSP>
    {
        public Log4netCSPDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netHSMDisablesForwardingTestsNetCoreLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
    {
        public Log4netHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }
    [NetCoreTest]
    public class Log4netCSPDisablesForwardingTestsNetCoreLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestCSP>
    {
        public Log4netCSPDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region MicrosoftLogging

    [NetCoreTest]
    public class MicrosoftLoggingHSMDisablesForwardingTestsNetCoreLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
    {
        public MicrosoftLoggingHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }
    [NetCoreTest]
    public class MicrosoftLoggingCSPDisablesForwardingTestsNetCoreLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestCSP>
    {
        public MicrosoftLoggingCSPDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class MicrosoftLoggingCSPDisablesForwardingTestsFWLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
    {
        public MicrosoftLoggingCSPDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class SerilogHSMDisablesForwardingTestsFWLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
    {
        public SerilogHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogCSPDisablesForwardingTestsFWLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestCSP>
    {
        public SerilogCSPDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogHSMDisablesForwardingTestsNetCoreLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
    {
        public SerilogHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }
    [NetCoreTest]
    public class SerilogCSPDisablesForwardingTestsNetCoreLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestCSP>
    {
        public SerilogCSPDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region NLog

    [NetFrameworkTest]
    public class NLogHSMDisablesForwardingTestsFWLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
    {
        public NLogHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogCSPDisablesForwardingTestsFWLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestCSP>
    {
        public NLogCSPDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogHSMDisablesForwardingTestsNetCoreLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
    {
        public NLogHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }
    [NetCoreTest]
    public class NLogCSPDisablesForwardingTestsNetCoreLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestCSP>
    {
        public NLogCSPDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    #endregion

    #region Sitecore

    [NetFrameworkTest]
    public class SitecoreHSMDisablesForwardingTestsFWLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
    {
        public SitecoreHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Sitecore)
        {
        }
    }

    [NetFrameworkTest]
    public class SitecoreCSPDisablesForwardingTestsFWLatestTests : HSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestCSP>
    {
        public SitecoreCSPDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Sitecore)
        {
        }
    }

    #endregion

}
