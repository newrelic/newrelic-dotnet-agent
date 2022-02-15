// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class Log4NetMetricsAndForwardingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private bool _metricsEnabled;
        private bool _forwardingEnabled;

        private const string OutsideTransactionDebugMessage = "OutsideTransactionDebugLogMessage";
        private const string OutsideTransactionInfoMessage = "OutsideTransactionInfoLogMessage";
        private const string OutsideTransactionWarningMessage = "OutsideTransactionWarningLogMessage";
        private const string OutsideTransactionErrorMessage = "OutsideTransactionErrorLogMessage";
        private const string OutsideTransactionFatalMessage = "OutsideTransactionFatalLogMessage";

        private const string InTransactionDebugMessage = "InTransactionDebugLogMessage";
        private const string InTransactionInfoMessage = "InTransactionInfoLogMessage";
        private const string InTransactionWarningMessage = "InTransactionWarningLogMessage";
        private const string InTransactionErrorMessage = "InTransactionErrorLogMessage";
        private const string InTransactionFatalMessage = "InTransactionFatalLogMessage";


        public Log4NetMetricsAndForwardingTestsBase(TFixture fixture, ITestOutputHelper output, bool metricsEnabled, bool forwardingEnabled) : base(fixture)
        {
            _fixture = fixture;
            _metricsEnabled = metricsEnabled;
            _forwardingEnabled = forwardingEnabled;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"Log4netTester Configure");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {OutsideTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {OutsideTransactionInfoMessage} INFO");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {OutsideTransactionWarningMessage} WARN");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {OutsideTransactionErrorMessage} ERROR");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {OutsideTransactionFatalMessage} FATAL");

            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InTransactionInfoMessage} INFO");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InTransactionWarningMessage} WARN");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InTransactionErrorMessage} ERROR");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InTransactionFatalMessage} FATAL");

            // This is necessary for the data usage metric assertions to work.  Only need to do it if forwarding is enabled.
            if (_forwardingEnabled)
            {
                _fixture.AddCommand($"RootCommands DelaySeconds 60");
            }

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier.EnableLogMetrics(metricsEnabled)
                    .EnableLogForwarding(forwardingEnabled)
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void LogLinesPerLevelMetricsExist()
        {
            var loggingMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Logging/lines/DEBUG", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/INFO", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/WARN", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/ERROR", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/FATAL", callCount = 2 },

                new Assertions.ExpectedMetric { metricName = "Logging/lines", callCount = 10 },
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            if (_metricsEnabled)
            {
                Assertions.MetricsExist(loggingMetrics, actualMetrics);
            }
            else
            {
                Assertions.MetricsDoNotExist(loggingMetrics, actualMetrics);
            }
        }

        [Fact]
        public void SupportabilityDataUsageMetricsExist()
        {
            var logEventDataUsageMetricName = "Supportability/DotNET/Collector/log_event_data/Output/Bytes";

            var logEventDataUsageMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = logEventDataUsageMetricName}
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            if (_forwardingEnabled)
            {
                Assertions.MetricsExist(logEventDataUsageMetrics, actualMetrics);
                var logEventDataMetrics = actualMetrics.Where(x => x.MetricSpec.Name == logEventDataUsageMetricName);
                foreach (var metric in logEventDataMetrics)
                {
                    Assert.NotEqual(0UL, metric.Values.CallCount);
                    Assert.NotEqual(0, metric.Values.Total);
                }
            }
            else
            {
                Assertions.MetricsDoNotExist(logEventDataUsageMetrics, actualMetrics);
            }
        }

        [Fact]
        public void CountsAndValuesAreAsExpected()
        {
            var logEventData = _fixture.AgentLog.GetLogEventData().FirstOrDefault();
            if (_forwardingEnabled)
            {
                Assert.NotNull(logEventData);
                Assert.NotNull(logEventData.Common);
                Assert.NotNull(logEventData.Common.Attributes);
                Assert.False(string.IsNullOrWhiteSpace(logEventData.Common.Attributes.EntityGuid));
                Assert.False(string.IsNullOrWhiteSpace(logEventData.Common.Attributes.EntityName));
                Assert.False(string.IsNullOrWhiteSpace(logEventData.Common.Attributes.EntityType));
                Assert.False(string.IsNullOrWhiteSpace(logEventData.Common.Attributes.Hostname));
                Assert.Equal("nr-dotnet-agent", logEventData.Common.Attributes.PluginType);
            }
            else
            {
                Assert.Null(logEventData);
            }

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();
            if (_forwardingEnabled)
            {
                Assert.Equal(10, logLines.Length);

                foreach (var logLine in logLines)
                {
                    Assert.False(string.IsNullOrWhiteSpace(logLine.Message));
                    Assert.False(string.IsNullOrWhiteSpace(logLine.Level));
                    Assert.NotEqual(0, logLine.Timestamp);
                }
            }
            else
            {
                Assert.Empty(logLines);
            }
        }

        [Fact]
        public void LoggingWorksInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = InTransactionDebugMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = InTransactionInfoMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "WARN", LogMessage = InTransactionWarningMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = InTransactionErrorMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = InTransactionFatalMessage, HasTraceId = true, HasSpanId = true }
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("InTransaction")).Count());
            }
        }

        [Fact]
        public void LoggingWorksOutsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = OutsideTransactionDebugMessage},
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = OutsideTransactionInfoMessage},
                new Assertions.ExpectedLogLine { LogLevel = "WARN", LogMessage = OutsideTransactionWarningMessage},
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = OutsideTransactionErrorMessage},
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = OutsideTransactionFatalMessage},
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("OutsideTransaction")).Count());
            }
        }

    }

    #region Metrics and Forwarding Enabled
    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingEnabledTestsFWLatestTests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, true)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingEnabledTestsFW471Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingEnabledTestsFW462Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCoreLatestTests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCore50Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCore31Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCore21Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true)
        {
        }
    }
    #endregion

    #region Metrics and Forwarding Disabled
    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingDisabledTestsFWLatestTests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, false)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingDisabledTestsFW471Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingDisabledTestsFW462Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCoreLatestTests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCore50Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCore31Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCore21Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false)
        {
        }
    }

    #endregion

    #region Metrics Enabled, Forwarding Disabled
    [NetFrameworkTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsFWLatestTests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, false)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsFW471Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsFW462Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore50Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore31Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore21Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false)
        {
        }
    }

    #endregion

    #region Metrics Disabled, Forwarding Enabled
    [NetFrameworkTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsFWLatestTests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, true)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsFW471Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsFW462Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore50Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore31Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore22Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore21Tests : Log4NetMetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true)
        {
        }
    }

    #endregion

}
