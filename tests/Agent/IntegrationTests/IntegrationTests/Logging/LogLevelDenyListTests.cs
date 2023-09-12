// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.MetricsAndForwarding
{
    public abstract class LogLevelDenyListTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private LoggingFramework _loggingFramework;


        public LogLevelDenyListTestsBase(TFixture fixture, ITestOutputHelper output,
            LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _loggingFramework = loggingFramework;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            //_fixture.AddCommand("RootCommands LaunchDebugger");
            _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework} {RandomPortGenerator.NextPort()}");
            _fixture.AddCommand("LoggingTester Configure");

            _fixture.AddCommand("LoggingTester CreateSingleLogMessage DebugMessage DEBUG");
            _fixture.AddCommand("LoggingTester CreateSingleLogMessage InfoMessage INFO");
            _fixture.AddCommand("LoggingTester CreateSingleLogMessage WarningMessage WARNING");
            _fixture.AddCommand("LoggingTester CreateSingleLogMessage ErrorMessage ERROR");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                        .EnableApplicationLogging()
                        .EnableLogForwarding()
                        .SetLogForwardingLogLevelDenyList($"{LogUtils.GetLevelName(_loggingFramework, "DEBUG")},{LogUtils.GetLevelName(_loggingFramework, "INFO")}")
                        .SetLogLevel("debug");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegex, TimeSpan.FromSeconds(30));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void LoggingMetricsExist()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + LogUtils.GetLevelName(_loggingFramework, "WARN"), callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + LogUtils.GetLevelName(_loggingFramework, "ERROR"), callCount = 1 },

                new Assertions.ExpectedMetric { metricName = "Logging/lines", callCount = 2 },

                new Assertions.ExpectedMetric { metricName = "Logging/denied/" + LogUtils.GetLevelName(_loggingFramework, "DEBUG"), callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/denied/" + LogUtils.GetLevelName(_loggingFramework, "INFO"), callCount = 1 },

                new Assertions.ExpectedMetric { metricName = "Logging/denied", callCount = 2 },
            };
            var notExpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + LogUtils.GetLevelName(_loggingFramework, "DEBUG") },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + LogUtils.GetLevelName(_loggingFramework, "INFO") }
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics().ToList();
            Assertions.MetricsExist(expectedMetrics, actualMetrics);
            Assertions.MetricsDoNotExist(notExpectedMetrics, actualMetrics);
        }

    }
    #region log4net

    [NetFrameworkTest]
    public class Log4NetLogLevelDenyListTestsFWLatestTests : LogLevelDenyListTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetLogLevelDenyListTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetLogLevelDenyListTestsFW471Tests : LogLevelDenyListTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetLogLevelDenyListTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetLogLevelDenyListTestsFW462Tests : LogLevelDenyListTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetLogLevelDenyListTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetLogLevelDenyListTestsNetCoreLatestTests : LogLevelDenyListTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetLogLevelDenyListTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetLogLevelDenyListTestsNetCoreOldestTests : LogLevelDenyListTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4NetLogLevelDenyListTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }
    #endregion

    #region MEL

    [NetCoreTest]
    public class MELLogLevelDenyListTestsNetCoreLatestTests : LogLevelDenyListTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MELLogLevelDenyListTestsNetCoreLatestTests(
            ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class
        MELLogLevelDenyListTestsNetCoreOldestTests : LogLevelDenyListTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MELLogLevelDenyListTestsNetCoreOldestTests(
            ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class
    MELLogLevelDenyListTestsFWLatestTests : LogLevelDenyListTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MELLogLevelDenyListTestsFWLatestTests(
            ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class
        SerilogLogLevelDenyListTestsFWLatestTests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogLogLevelDenyListTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        SerilogLogLevelDenyListTestsFW471Tests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogLogLevelDenyListTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        SerilogLogLevelDenyListTestsFW462Tests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogLogLevelDenyListTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class
        SerilogLogLevelDenyListTestsNetCoreLatestTests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogLogLevelDenyListTestsNetCoreLatestTests(
            ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class
        SerilogLogLevelDenyListTestsNetCoreOldestTests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SerilogLogLevelDenyListTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region NLog

    [NetFrameworkTest]
    public class
        NLogLogLevelDenyListTestsFWLatestTests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogLogLevelDenyListTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        NLogLogLevelDenyListTestsFW471Tests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFW471>
    {
        public NLogLogLevelDenyListTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        NLogLogLevelDenyListTestsFW462Tests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFW462>
    {
        public NLogLogLevelDenyListTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class
        NLogLogLevelDenyListTestsNetCoreLatestTests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogLogLevelDenyListTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class
        NLogLogLevelDenyListTestsNetCoreOldestTests : LogLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogLogLevelDenyListTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    #endregion
}
