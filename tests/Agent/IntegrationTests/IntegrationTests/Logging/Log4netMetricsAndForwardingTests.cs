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

        private const string AsyncOutsideTransactionDebugMessage = "AsyncOutsideTransactionDebugLogMessage";
        private const string AsyncOutsideTransactionInfoMessage = "AsyncOutsideTransactionInfoLogMessage";
        private const string AsyncOutsideTransactionWarningMessage = "AsyncOutsideTransactionWarningLogMessage";
        private const string AsyncOutsideTransactionErrorMessage = "AsyncOutsideTransactionErrorLogMessage";
        private const string AsyncOutsideTransactionFatalMessage = "AsyncOutsideTransactionFatalLogMessage";

        private const string AsyncNoAwaitOutsideTransactionDebugMessage = "AsyncNoAwaitOutsideTransactionDebugLogMessage";
        private const string AsyncNoAwaitOutsideTransactionInfoMessage = "AsyncNoAwaitOutsideTransactionInfoLogMessage";
        private const string AsyncNoAwaitOutsideTransactionWarningMessage = "AsyncNoAwaitOutsideTransactionWarningLogMessage";
        private const string AsyncNoAwaitOutsideTransactionErrorMessage = "AsyncNoAwaitOutsideTransactionErrorLogMessage";
        private const string AsyncNoAwaitOutsideTransactionFatalMessage = "AsyncNoAwaitOutsideTransactionFatalLogMessage";

        private const string InTransactionDebugMessage = "InTransactionDebugLogMessage";
        private const string InTransactionInfoMessage = "InTransactionInfoLogMessage";
        private const string InTransactionWarningMessage = "InTransactionWarningLogMessage";
        private const string InTransactionErrorMessage = "InTransactionErrorLogMessage";
        private const string InTransactionFatalMessage = "InTransactionFatalLogMessage";

        private const string AsyncInTransactionDebugMessage = "AsyncInTransactionDebugLogMessage";
        private const string AsyncInTransactionInfoMessage = "AsyncInTransactionInfoLogMessage";
        private const string AsyncInTransactionWarningMessage = "AsyncInTransactionWarningLogMessage";
        private const string AsyncInTransactionErrorMessage = "AsyncInTransactionErrorLogMessage";
        private const string AsyncInTransactionFatalMessage = "AsyncInTransactionFatalLogMessage";

        private const string AsyncNoAwaitInTransactionDebugMessage = "AsyncNoAwaitInTransactionDebugLogMessage";
        private const string AsyncNoAwaitInTransactionInfoMessage = "AsyncNoAwaitInTransactionInfoLogMessage";
        private const string AsyncNoAwaitInTransactionWarningMessage = "AsyncNoAwaitInTransactionWarningLogMessage";
        private const string AsyncNoAwaitInTransactionErrorMessage = "AsyncNoAwaitInTransactionErrorLogMessage";
        private const string AsyncNoAwaitInTransactionFatalMessage = "AsyncNoAwaitInTransactionFatalLogMessage";

        private const string AsyncNoAwaitWithDelayInTransactionDebugMessage = "AsyncNoAwaitWithDelayInTransactionDebugLogMessage";
        private const string AsyncNoAwaitWithDelayInTransactionInfoMessage = "AsyncNoAwaitWithDelayInTransactionInfoLogMessage";
        private const string AsyncNoAwaitWithDelayInTransactionWarningMessage = "AsyncNoAwaitWithDelayInTransactionWarningLogMessage";
        private const string AsyncNoAwaitWithDelayInTransactionErrorMessage = "AsyncNoAwaitWithDelayInTransactionErrorLogMessage";
        private const string AsyncNoAwaitWithDelayInTransactionFatalMessage = "AsyncNoAwaitWithDelayInTransactionFatalLogMessage";

        private const string TraceAttributeOutsideTransactionLogMessage = "TraceAttributeOutsideTransactionLogMessage";
        private const string DifferentTraceAttributesInsideTransactionLogMessage = "DifferentTraceAttributesInsideTransactionLogMessage";

        public Log4NetMetricsAndForwardingTestsBase(TFixture fixture, ITestOutputHelper output, bool metricsEnabled, bool forwardingEnabled) : base(fixture)
        {
            _fixture = fixture;
            _metricsEnabled = metricsEnabled;
            _forwardingEnabled = forwardingEnabled;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework log4net");
            _fixture.AddCommand($"LoggingTester Configure");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {OutsideTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {OutsideTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {OutsideTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {OutsideTransactionErrorMessage} ERROR");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {OutsideTransactionFatalMessage} FATAL");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsync {AsyncOutsideTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsync {AsyncOutsideTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsync {AsyncOutsideTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsync {AsyncOutsideTransactionErrorMessage} ERROR");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsync {AsyncOutsideTransactionFatalMessage} FATAL");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsyncNoAwait {AsyncNoAwaitOutsideTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsyncNoAwait {AsyncNoAwaitOutsideTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsyncNoAwait {AsyncNoAwaitOutsideTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsyncNoAwait {AsyncNoAwaitOutsideTransactionErrorMessage} ERROR");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsyncNoAwait {AsyncNoAwaitOutsideTransactionFatalMessage} FATAL");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {InTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {InTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {InTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {InTransactionErrorMessage} ERROR");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {InTransactionFatalMessage} FATAL");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsync {AsyncInTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsync {AsyncInTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsync {AsyncInTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsync {AsyncInTransactionErrorMessage} ERROR");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsync {AsyncInTransactionFatalMessage} FATAL");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwait {AsyncNoAwaitInTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwait {AsyncNoAwaitInTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwait {AsyncNoAwaitInTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwait {AsyncNoAwaitInTransactionErrorMessage} ERROR");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwait {AsyncNoAwaitInTransactionFatalMessage} FATAL");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay {AsyncNoAwaitWithDelayInTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay {AsyncNoAwaitWithDelayInTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay {AsyncNoAwaitWithDelayInTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay {AsyncNoAwaitWithDelayInTransactionErrorMessage} ERROR");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay {AsyncNoAwaitWithDelayInTransactionFatalMessage} FATAL");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageWithTraceAttribute {TraceAttributeOutsideTransactionLogMessage} INFO");

            _fixture.AddCommand($"LoggingTester CreateTwoLogMessagesInTransactionWithDifferentTraceAttributes {DifferentTraceAttributesInsideTransactionLogMessage} INFO");

            // Give the unawaited async logs some time to catch up
            _fixture.AddCommand($"RootCommands DelaySeconds 5");

            // This is necessary for the data usage metric assertions to work.  Only need to do it if forwarding is enabled.
            if (_forwardingEnabled)
            {
                _fixture.AddCommand($"RootCommands DelaySeconds 55");
            }

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier.EnableApplicationLogging()
                    .EnableLogMetrics(metricsEnabled)
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
                new Assertions.ExpectedMetric { metricName = "Logging/lines/DEBUG", callCount = 7 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/INFO", callCount = 10 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/WARN", callCount = 7 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/ERROR", callCount = 7 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/FATAL", callCount = 7 },

                new Assertions.ExpectedMetric { metricName = "Logging/lines", callCount = 38 },
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
        public void SupportabilityForwardingConfigurationMetricExists()
        {
            var actualMetrics = _fixture.AgentLog.GetMetrics();
            if (_forwardingEnabled)
            {
                Assert.Contains(actualMetrics, x => x.MetricSpec.Name == "Supportability/Logging/Forwarding/DotNET/enabled");
            }
            else
            {
                Assert.Contains(actualMetrics, x => x.MetricSpec.Name == "Supportability/Logging/Forwarding/DotNET/disabled");
            }
        }

        [Fact]
        public void SupportabilityMetricsConfigurationMetricExists()
        {
            var actualMetrics = _fixture.AgentLog.GetMetrics();
            if (_metricsEnabled)
            {
                Assert.Contains(actualMetrics, x => x.MetricSpec.Name == "Supportability/Logging/Metrics/DotNET/enabled");
            }
            else
            {
                Assert.Contains(actualMetrics, x => x.MetricSpec.Name == "Supportability/Logging/Metrics/DotNET/disabled");
            }
        }

        [Fact]
        public void SupportabilityLoggingFrameworkMetricExists()
        {
            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assert.Contains(actualMetrics, x => x.MetricSpec.Name == "Supportability/Logging/enabled/DotNET/log4net");
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
                Assert.False(string.IsNullOrWhiteSpace(logEventData.Common.Attributes.Hostname));
            }
            else
            {
                Assert.Null(logEventData);
            }

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();
            if (_forwardingEnabled)
            {
                Assert.Equal(38, logLines.Length);

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
        public void LoggingWorksWithTraceAttributeOutsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = TraceAttributeOutsideTransactionLogMessage, HasTraceId = false, HasSpanId = false }
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("TraceAttributeOutsideTransaction")).Count());
            }
        }

        [Fact]
        public void LoggingWorksWithDifferentTraceAttributesInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = DifferentTraceAttributesInsideTransactionLogMessage, HasTraceId = true, HasSpanId = true }
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                var logsOfInterest = logLines.Where(x => x.Message.StartsWith("DifferentTraceAttributesInsideTransaction")).ToArray();

                Assert.Equal(2, logsOfInterest.Length);
                Assert.NotEqual(logsOfInterest[0].Attributes.Spanid, logsOfInterest[1].Attributes.Spanid);
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
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = InTransactionFatalMessage, HasTraceId = true, HasSpanId = true },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("InTransaction")).Count());
            }
        }

        [Fact]
        public void AsyncLoggingWorksInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = AsyncInTransactionDebugMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = AsyncInTransactionInfoMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "WARN", LogMessage = AsyncInTransactionWarningMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = AsyncInTransactionErrorMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = AsyncInTransactionFatalMessage, HasTraceId = true, HasSpanId = true },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("AsyncInTransaction")).Count());
            }
        }

        [Fact]
        public void AsyncNoAwaitLoggingWorksInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                // NOTE: since the log is not awaited, the logs intermittently show up in/out of a transaction.
                // Because of this the spanId/traceId members are not checked by not specifying true/false in the ExpectedLogLines
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = AsyncNoAwaitInTransactionDebugMessage},
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = AsyncNoAwaitInTransactionInfoMessage},
                new Assertions.ExpectedLogLine { LogLevel = "WARN", LogMessage = AsyncNoAwaitInTransactionWarningMessage},
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = AsyncNoAwaitInTransactionErrorMessage},
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = AsyncNoAwaitInTransactionFatalMessage},
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("AsyncNoAwaitInTransaction")).Count());
            }
        }

        [Fact]
        public void LoggingWorksOutsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = OutsideTransactionDebugMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = OutsideTransactionInfoMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "WARN", LogMessage = OutsideTransactionWarningMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = OutsideTransactionErrorMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = OutsideTransactionFatalMessage, HasSpanId = false, HasTraceId = false},
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("OutsideTransaction")).Count());
            }
        }

        [Fact]
        public void AsyncLoggingWorksOutsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = AsyncOutsideTransactionDebugMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = AsyncOutsideTransactionInfoMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "WARN", LogMessage = AsyncOutsideTransactionWarningMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = AsyncOutsideTransactionErrorMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = AsyncOutsideTransactionFatalMessage, HasSpanId = false, HasTraceId = false},
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("AsyncOutsideTransaction")).Count());
            }
        }

        [Fact]
        public void AsyncNoAwaitLoggingWorksOutsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = AsyncNoAwaitOutsideTransactionDebugMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = AsyncNoAwaitOutsideTransactionInfoMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "WARN", LogMessage = AsyncNoAwaitOutsideTransactionWarningMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = AsyncNoAwaitOutsideTransactionErrorMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = AsyncNoAwaitOutsideTransactionFatalMessage, HasSpanId = false, HasTraceId = false},
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("AsyncNoAwaitOutsideTransaction")).Count());
            }
        }

        [Fact]
        public void AsyncNoAwaitWithDelayLoggingWorksInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = AsyncNoAwaitWithDelayInTransactionDebugMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = AsyncNoAwaitWithDelayInTransactionInfoMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "WARN", LogMessage = AsyncNoAwaitWithDelayInTransactionWarningMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = AsyncNoAwaitWithDelayInTransactionErrorMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = AsyncNoAwaitWithDelayInTransactionFatalMessage, HasTraceId = true, HasSpanId = true },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("AsyncNoAwaitWithDelayInTransaction")).Count());
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
