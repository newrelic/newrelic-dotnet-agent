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
    public abstract class MetricsAndForwardingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private bool _metricsEnabled;
        private bool _forwardingEnabled;
        private LoggingFramework _loggingFramework;

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

        public MetricsAndForwardingTestsBase(TFixture fixture, ITestOutputHelper output, bool metricsEnabled, bool forwardingEnabled, LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _metricsEnabled = metricsEnabled;
            _forwardingEnabled = forwardingEnabled;
            _loggingFramework = loggingFramework;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework}");
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
            _fixture.AddCommand($"RootCommands DelaySeconds 10");

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
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + GetLogLevelName(_loggingFramework, "DEBUG"), callCount = 7 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + GetLogLevelName(_loggingFramework, "INFO"), callCount = 10 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + GetLogLevelName(_loggingFramework, "WARN"), callCount = 7 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + GetLogLevelName(_loggingFramework, "ERROR"), callCount = 7 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + GetLogLevelName(_loggingFramework, "FATAL"), callCount = 7 },

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
            var expectedFrameworkName = GetFrameworkName(_loggingFramework);
            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assert.Contains(actualMetrics, x => x.MetricSpec.Name == $"Supportability/Logging/DotNET/{expectedFrameworkName}/enabled");
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
                    Assert.False(string.IsNullOrWhiteSpace(logLine.LogLevel));
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
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "INFO"), LogMessage = TraceAttributeOutsideTransactionLogMessage, HasTraceId = false, HasSpanId = false }
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
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "INFO"), LogMessage = DifferentTraceAttributesInsideTransactionLogMessage, HasTraceId = true, HasSpanId = true }
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
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "DEBUG"), LogMessage = InTransactionDebugMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "INFO"), LogMessage = InTransactionInfoMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "WARN"), LogMessage = InTransactionWarningMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "ERROR"), LogMessage = InTransactionErrorMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "FATAL"), LogMessage = InTransactionFatalMessage, HasTraceId = true, HasSpanId = true },
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
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncInTransactionDebugMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "INFO"), LogMessage = AsyncInTransactionInfoMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "WARN"), LogMessage = AsyncInTransactionWarningMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncInTransactionErrorMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "FATAL"), LogMessage = AsyncInTransactionFatalMessage, HasTraceId = true, HasSpanId = true },
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
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncNoAwaitInTransactionDebugMessage},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "INFO"), LogMessage = AsyncNoAwaitInTransactionInfoMessage},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "WARN"), LogMessage = AsyncNoAwaitInTransactionWarningMessage},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncNoAwaitInTransactionErrorMessage},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "FATAL"), LogMessage = AsyncNoAwaitInTransactionFatalMessage},
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
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "DEBUG"), LogMessage = OutsideTransactionDebugMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "INFO"), LogMessage = OutsideTransactionInfoMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "WARN"), LogMessage = OutsideTransactionWarningMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "ERROR"), LogMessage = OutsideTransactionErrorMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "FATAL"), LogMessage = OutsideTransactionFatalMessage, HasSpanId = false, HasTraceId = false},
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
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncOutsideTransactionDebugMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "INFO"), LogMessage = AsyncOutsideTransactionInfoMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "WARN"), LogMessage = AsyncOutsideTransactionWarningMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncOutsideTransactionErrorMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "FATAL"), LogMessage = AsyncOutsideTransactionFatalMessage, HasSpanId = false, HasTraceId = false},
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
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncNoAwaitOutsideTransactionDebugMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "INFO"), LogMessage = AsyncNoAwaitOutsideTransactionInfoMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "WARN"), LogMessage = AsyncNoAwaitOutsideTransactionWarningMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncNoAwaitOutsideTransactionErrorMessage, HasSpanId = false, HasTraceId = false},
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "FATAL"), LogMessage = AsyncNoAwaitOutsideTransactionFatalMessage, HasSpanId = false, HasTraceId = false},
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
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncNoAwaitWithDelayInTransactionDebugMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "INFO"), LogMessage = AsyncNoAwaitWithDelayInTransactionInfoMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "WARN"), LogMessage = AsyncNoAwaitWithDelayInTransactionWarningMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncNoAwaitWithDelayInTransactionErrorMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = GetLogLevelName(_loggingFramework, "FATAL"), LogMessage = AsyncNoAwaitWithDelayInTransactionFatalMessage, HasTraceId = true, HasSpanId = true },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => x.Message.StartsWith("AsyncNoAwaitWithDelayInTransaction")).Count());
            }
        }

        private string GetFrameworkName(LoggingFramework loggingFramework)
        {
            switch (loggingFramework)
            {
                case LoggingFramework.Log4net:
                    return "log4net";
                case LoggingFramework.MicrosoftLogging:
                    return "MicrosoftLogging";
                case LoggingFramework.Serilog:
                    return "serilog";
                default:
                    return "unknown";
            }
        }

        private string GetLogLevelName(LoggingFramework loggingFramework, string level)
        {
            switch (loggingFramework)
            {
                // log4net names are the same as our internal names
                case LoggingFramework.Log4net:
                    return level;
                case LoggingFramework.MicrosoftLogging:
                    switch (level)
                    {
                        case "DEBUG":
                            return "DEBUG";
                        case "INFO":
                            return "INFORMATION";
                        case "WARN":
                            return "WARNING";
                        case "ERROR":
                            return "ERROR";
                        case "FATAL":
                            return "TRACE";
                        default:
                            return level;
                    }
                case LoggingFramework.Serilog:
                    switch (level)
                    {
                        case "DEBUG":
                            return "DEBUG";
                        case "INFO":
                            return "INFORMATION";
                        case "WARN":
                            return "WARNING";
                        case "ERROR":
                            return "ERROR";
                        case "FATAL":
                            return "FATAL";
                        default:
                            return level;
                    }
            }
            return string.Empty;
        }


    }

    #region log4net
    #region Metrics and Forwarding Enabled
    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Log4net)
        {
        }
    }
    #endregion

    #region Metrics and Forwarding Disabled
    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region Metrics Enabled, Forwarding Disabled
    [NetFrameworkTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region Metrics Disabled, Forwarding Enabled
    [NetFrameworkTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore22Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #endregion

    #region MicrosoftLogging

    #region Metrics and Forwarding Enabled

    [NetCoreTest]
    public class MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }
    #endregion

    #region Metrics and Forwarding Disabled

    [NetCoreTest]
    public class MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Metrics Enabled, Forwarding Disabled

    [NetCoreTest]
    public class MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Metrics Disabled, Forwarding Enabled

    [NetCoreTest]
    public class MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore22Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion


    #endregion

    #region Serilog

    #region Metrics and Forwarding Enabled
    [NetFrameworkTest]
    public class SerilogMetricsAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogMetricsAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMetricsAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogMetricsAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMetricsAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogMetricsAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogMetricsAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogMetricsAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogMetricsAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsAndForwardingEnabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public SerilogMetricsAndForwardingEnabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, true, true, LoggingFramework.Serilog)
        {
        }
    }
    #endregion

    #region Metrics and Forwarding Disabled
    [NetFrameworkTest]
    public class SerilogMetricsAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogMetricsAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMetricsAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogMetricsAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMetricsAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogMetricsAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogMetricsAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogMetricsAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogMetricsAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsAndForwardingDisabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public SerilogMetricsAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, false, false, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region Metrics Enabled, Forwarding Disabled
    [NetFrameworkTest]
    public class SerilogMetricsEnabledAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogMetricsEnabledAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMetricsEnabledAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogMetricsEnabledAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMetricsEnabledAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogMetricsEnabledAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsEnabledAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogMetricsEnabledAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsEnabledAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogMetricsEnabledAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsEnabledAndForwardingDisabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public SerilogMetricsEnabledAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, true, false, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region Metrics Disabled, Forwarding Enabled
    [NetFrameworkTest]
    public class SerilogMetricsDisabledAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogMetricsDisabledAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMetricsDisabledAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogMetricsDisabledAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMetricsDisabledAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogMetricsDisabledAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsDisabledAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogMetricsDisabledAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsDisabledAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogMetricsDisabledAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsDisabledAndForwardingEnabledTestsNetCore22Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public SerilogMetricsDisabledAndForwardingEnabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMetricsDisabledAndForwardingEnabledTestsNetCore21Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public SerilogMetricsDisabledAndForwardingEnabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output, false, true, LoggingFramework.Serilog)
        {
        }
    }

    #endregion


    #endregion

}
