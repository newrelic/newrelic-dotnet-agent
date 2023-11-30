// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.ZeroMaxSamplesStored
{
    public abstract class ZeroMaxSamplesStoredTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public ZeroMaxSamplesStoredTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
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
                    .SetLogForwardingMaxSamplesStored(1) // must be 1 since 0 causes it to return the default
                    .SetLogLevel("debug");
                    configModifier.DisableEventListenerSamplers(); // Required for .NET 8 to pass.
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void NoLogLinesSent()
        {
            var logData = _fixture.AgentLog.GetLogEventData().FirstOrDefault();
            Assert.Null(logData);
        }
    }

    #region log4net

    [NetFrameworkTest]
    public class Log4netZeroMaxSamplesStoredTestsFWLatestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netZeroMaxSamplesStoredTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netZeroMaxSamplesStoredTestsFW471Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netZeroMaxSamplesStoredTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netZeroMaxSamplesStoredTestsFW462Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netZeroMaxSamplesStoredTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netZeroMaxSamplesStoredTestsNetCoreLatestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netZeroMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netZeroMaxSamplesStoredTestsNetCoreOldestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4netZeroMaxSamplesStoredTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region MicrosoftLogging

    [NetCoreTest]
    public class MicrosoftLoggingZeroMaxSamplesStoredTestsNetCoreLatestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingZeroMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingZeroMaxSamplesStoredTestsNetCoreOldestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MicrosoftLoggingZeroMaxSamplesStoredTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class MicrosoftLoggingZeroMaxSamplesStoredTestsFWLatestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MicrosoftLoggingZeroMaxSamplesStoredTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class SerilogZeroMaxSamplesStoredTestsFWLatestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogZeroMaxSamplesStoredTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogZeroMaxSamplesStoredTestsFW471Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogZeroMaxSamplesStoredTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogZeroMaxSamplesStoredTestsFW462Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogZeroMaxSamplesStoredTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogZeroMaxSamplesStoredTestsNetCoreLatestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogZeroMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogZeroMaxSamplesStoredTestsNetCoreOldestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SerilogZeroMaxSamplesStoredTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region NLog

    [NetFrameworkTest]
    public class NLogZeroMaxSamplesStoredTestsFWLatestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogZeroMaxSamplesStoredTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogZeroMaxSamplesStoredTestsFW471Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogZeroMaxSamplesStoredTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogZeroMaxSamplesStoredTestsFW462Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public NLogZeroMaxSamplesStoredTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogZeroMaxSamplesStoredTestsNetCoreLatestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogZeroMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogZeroMaxSamplesStoredTestsNetCoreOldestTests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogZeroMaxSamplesStoredTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    #endregion

}
