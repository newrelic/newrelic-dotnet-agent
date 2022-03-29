// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
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

                    configModifier
                    .EnableApplicationLogging()
                    .EnableLogForwarding()
                    .EnableLogMetrics()
                    .SetLogForwardingMaxSamplesStored(1) // must be 1 since 0 causes it to return the default
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
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
    public class Log4netZeroMaxSamplesStoredTestsNetCore50Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netZeroMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netZeroMaxSamplesStoredTestsNetCore31Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netZeroMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netZeroMaxSamplesStoredTestsNetCore22Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public Log4netZeroMaxSamplesStoredTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netZeroMaxSamplesStoredTestsNetCore21Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4netZeroMaxSamplesStoredTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
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
    public class MicrosoftLoggingZeroMaxSamplesStoredTestsNetCore50Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingZeroMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingZeroMaxSamplesStoredTestsNetCore31Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingZeroMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingZeroMaxSamplesStoredTestsNetCore22Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public MicrosoftLoggingZeroMaxSamplesStoredTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingZeroMaxSamplesStoredTestsNetCore21Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public MicrosoftLoggingZeroMaxSamplesStoredTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
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
    public class SerilogZeroMaxSamplesStoredTestsNetCore50Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogZeroMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogZeroMaxSamplesStoredTestsNetCore31Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogZeroMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogZeroMaxSamplesStoredTestsNetCore22Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public SerilogZeroMaxSamplesStoredTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogZeroMaxSamplesStoredTestsNetCore21Tests : ZeroMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public SerilogZeroMaxSamplesStoredTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

}
