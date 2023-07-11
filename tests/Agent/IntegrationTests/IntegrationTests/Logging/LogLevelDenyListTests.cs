// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.MetricsAndForwarding
{
    public abstract class logLevelDenyListTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private LoggingFramework _loggingFramework;


        public logLevelDenyListTestsBase(TFixture fixture, ITestOutputHelper output,
            LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _loggingFramework = loggingFramework;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            //_fixture.AddCommand("RootCommands LaunchDebugger");
            _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework}");
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
    public class Log4NetlogLevelDenyListTestsFWLatestTests : logLevelDenyListTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetlogLevelDenyListTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetlogLevelDenyListTestsFW471Tests : logLevelDenyListTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetlogLevelDenyListTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetlogLevelDenyListTestsFW462Tests : logLevelDenyListTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetlogLevelDenyListTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetlogLevelDenyListTestsNetCoreLatestTests : logLevelDenyListTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetlogLevelDenyListTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetlogLevelDenyListTestsNetCoreOldestTests : logLevelDenyListTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4NetlogLevelDenyListTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }
    #endregion

    #region MEL

    [NetCoreTest]
    public class MELlogLevelDenyListTestsNetCoreLatestTests : logLevelDenyListTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MELlogLevelDenyListTestsNetCoreLatestTests(
            ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class
        MELlogLevelDenyListTestsNetCoreOldestTests : logLevelDenyListTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MELlogLevelDenyListTestsNetCoreOldestTests(
            ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class
    MELlogLevelDenyListTestsFWLatestTests : logLevelDenyListTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MELlogLevelDenyListTestsFWLatestTests(
            ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class
        SeriloglogLevelDenyListTestsFWLatestTests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFWLatest>
    {
        public SeriloglogLevelDenyListTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        SeriloglogLevelDenyListTestsFW471Tests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFW471>
    {
        public SeriloglogLevelDenyListTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        SeriloglogLevelDenyListTestsFW462Tests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFW462>
    {
        public SeriloglogLevelDenyListTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class
        SeriloglogLevelDenyListTestsNetCoreLatestTests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SeriloglogLevelDenyListTestsNetCoreLatestTests(
            ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class
        SeriloglogLevelDenyListTestsNetCoreOldestTests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SeriloglogLevelDenyListTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region NLog

    [NetFrameworkTest]
    public class
        NLoglogLevelDenyListTestsFWLatestTests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLoglogLevelDenyListTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        NLoglogLevelDenyListTestsFW471Tests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFW471>
    {
        public NLoglogLevelDenyListTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        NLoglogLevelDenyListTestsFW462Tests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureFW462>
    {
        public NLoglogLevelDenyListTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class
        NLoglogLevelDenyListTestsNetCoreLatestTests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLoglogLevelDenyListTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class
        NLoglogLevelDenyListTestsNetCoreOldestTests : logLevelDenyListTestsBase<
            ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLoglogLevelDenyListTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    #endregion
}
