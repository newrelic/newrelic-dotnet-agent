// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.MetricsAndForwarding
{
    public abstract class MetricsAndForwardingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private bool _metricsEnabled;
        private bool _forwardingEnabled;
        private LoggingFramework _loggingFramework;
        private bool _canHaveLogsOutsideTransaction;

        // Non-async messages don't need to test more than one line
        // Async messages should be tested to make sure more than one works
        // Errors use exceptions so those need to be tested along with regular messages

        private const string OutsideTransactionInfoMessage = "OutsideTransactionInfoLogMessage";
        private const string OutsideTransactionErrorMessage = "OutsideTransactionErrorLogMessage";

        private const string AsyncOutsideTransactionDebugMessage = "AsyncOutsideTransactionDebugLogMessage";
        private const string AsyncOutsideTransactionInfoMessage = "AsyncOutsideTransactionInfoLogMessage";
        private const string AsyncOutsideTransactionWarningMessage = "AsyncOutsideTransactionWarningLogMessage";
        private const string AsyncOutsideTransactionErrorMessage = "AsyncOutsideTransactionErrorLogMessage";

        private const string AsyncNoAwaitOutsideTransactionDebugMessage = "AsyncNoAwaitOutsideTransactionDebugLogMessage";
        private const string AsyncNoAwaitOutsideTransactionInfoMessage = "AsyncNoAwaitOutsideTransactionInfoLogMessage";
        private const string AsyncNoAwaitOutsideTransactionWarningMessage = "AsyncNoAwaitOutsideTransactionWarningLogMessage";
        private const string AsyncNoAwaitOutsideTransactionErrorMessage = "AsyncNoAwaitOutsideTransactionErrorLogMessage";

        private const string InTransactionInfoMessage = "InTransactionInfoLogMessage";
        private const string InTransactionErrorMessage = "InTransactionErrorLogMessage";
        
        private const string AsyncInTransactionDebugMessage = "AsyncInTransactionDebugLogMessage";
        private const string AsyncInTransactionInfoMessage = "AsyncInTransactionInfoLogMessage";
        private const string AsyncInTransactionWarningMessage = "AsyncInTransactionWarningLogMessage";
        private const string AsyncInTransactionErrorMessage = "AsyncInTransactionErrorLogMessage";

        private const string AsyncNoAwaitInTransactionDebugMessage = "AsyncNoAwaitInTransactionDebugLogMessage";
        private const string AsyncNoAwaitInTransactionInfoMessage = "AsyncNoAwaitInTransactionInfoLogMessage";
        private const string AsyncNoAwaitInTransactionWarningMessage = "AsyncNoAwaitInTransactionWarningLogMessage";
        private const string AsyncNoAwaitInTransactionErrorMessage = "AsyncNoAwaitInTransactionErrorLogMessage";

        private const string AsyncNoAwaitWithDelayInTransactionDebugMessage = "AsyncNoAwaitWithDelayInTransactionDebugLogMessage";
        private const string AsyncNoAwaitWithDelayInTransactionInfoMessage = "AsyncNoAwaitWithDelayInTransactionInfoLogMessage";
        private const string AsyncNoAwaitWithDelayInTransactionWarningMessage = "AsyncNoAwaitWithDelayInTransactionWarningLogMessage";
        private const string AsyncNoAwaitWithDelayInTransactionErrorMessage = "AsyncNoAwaitWithDelayInTransactionErrorLogMessage";

        private const string TraceAttributeOutsideTransactionLogMessage = "TraceAttributeOutsideTransactionLogMessage";
        private const string DifferentTraceAttributesInsideTransactionLogMessage = "DifferentTraceAttributesInsideTransactionLogMessage";

        private const string OutsideTransactionErrorNoMessage = "OutsideTransactionErrorLogNoMessage";
        private const string InTransactionErrorNoMessage = "InTransactionErrorLogNoMessage";
        private const string SerilogWebErrorNoMessage = "Exception of type 'System.Exception' was thrown.";

        private const string ErrorStackValue = "at MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation.ExceptionBuilder.BuildException(String message)";

        private const string ErrorClassValue = "System.Exception";

        public MetricsAndForwardingTestsBase(TFixture fixture, ITestOutputHelper output, bool metricsEnabled, bool forwardingEnabled, bool canHaveLogsOutsideTransaction, LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _metricsEnabled = metricsEnabled;
            _forwardingEnabled = forwardingEnabled;
            _loggingFramework = loggingFramework;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;
            _canHaveLogsOutsideTransaction = canHaveLogsOutsideTransaction;

            _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework}");
            _fixture.AddCommand($"LoggingTester Configure");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {OutsideTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {OutsideTransactionErrorMessage} ERROR");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsync {AsyncOutsideTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsync {AsyncOutsideTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsync {AsyncOutsideTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsync {AsyncOutsideTransactionErrorMessage} ERROR");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsyncNoAwait {AsyncNoAwaitOutsideTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsyncNoAwait {AsyncNoAwaitOutsideTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsyncNoAwait {AsyncNoAwaitOutsideTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageAsyncNoAwait {AsyncNoAwaitOutsideTransactionErrorMessage} ERROR");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {InTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {InTransactionErrorMessage} ERROR");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsync {AsyncInTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsync {AsyncInTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsync {AsyncInTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsync {AsyncInTransactionErrorMessage} ERROR");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwait {AsyncNoAwaitInTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwait {AsyncNoAwaitInTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwait {AsyncNoAwaitInTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwait {AsyncNoAwaitInTransactionErrorMessage} ERROR");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay {AsyncNoAwaitWithDelayInTransactionDebugMessage} DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay {AsyncNoAwaitWithDelayInTransactionInfoMessage} INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay {AsyncNoAwaitWithDelayInTransactionWarningMessage} WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionAsyncNoAwaitWithDelay {AsyncNoAwaitWithDelayInTransactionErrorMessage} ERROR");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageWithTraceAttribute {TraceAttributeOutsideTransactionLogMessage} INFO");

            _fixture.AddCommand($"LoggingTester CreateTwoLogMessagesInTransactionWithDifferentTraceAttributes {DifferentTraceAttributesInsideTransactionLogMessage} INFO");

            // These tests will have an exception, but no message
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {OutsideTransactionErrorNoMessage} NOMESSAGE");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {InTransactionErrorNoMessage} NOMESSAGE");

            // Give the unawaited async logs some time to catch up
            _fixture.AddCommand($"RootCommands DelaySeconds 10");

            // AddActions() executes the applied actions after actions defined by the base.
            // In this case the base defines an exerciseApplication action we want to wait after.
            // Commentary: The actions defined by the base class do not impact failure/success
            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableLogMetrics(metricsEnabled)
                    .EnableLogForwarding(forwardingEnabled)
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                },
                exerciseApplication: () =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            LogLinesPerLevelMetricsExist();
            SupportabilityForwardingConfigurationMetricExists();
            SupportabilityMetricsConfigurationMetricExists();
            SupportabilityLoggingFrameworkMetricExists();
            CountsAndValuesAreAsExpected();
            LoggingWorksWithTraceAttributeOutsideTransaction();
            LoggingWorksWithDifferentTraceAttributesInsideTransaction();
            LoggingWorksInsideTransaction();
            AsyncLoggingWorksInsideTransaction();
            AsyncNoAwaitLoggingWorksInsideTransaction();
            LoggingWorksOutsideTransaction();
            AsyncLoggingWorksOutsideTransaction();
            AsyncNoAwaitLoggingWorksOutsideTransaction();
            AsyncNoAwaitWithDelayLoggingWorksInsideTransaction();
        }
        
        private void LogLinesPerLevelMetricsExist()
        {
            var loggingMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + LogUtils.GetLevelName(_loggingFramework, "DEBUG"), callCount = 5 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + LogUtils.GetLevelName(_loggingFramework, "INFO"), callCount = 10 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + LogUtils.GetLevelName(_loggingFramework, "WARN"), callCount = 5 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/" + LogUtils.GetLevelName(_loggingFramework, "ERROR"), callCount = 9 },

                new Assertions.ExpectedMetric { metricName = "Logging/lines", callCount = 29 },
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

        private void SupportabilityForwardingConfigurationMetricExists()
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

        private void SupportabilityMetricsConfigurationMetricExists()
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

        private void SupportabilityLoggingFrameworkMetricExists()
        {
            var expectedFrameworkName = LogUtils.GetFrameworkName(_loggingFramework);
            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assert.Contains(actualMetrics, x => x.MetricSpec.Name == $"Supportability/Logging/DotNET/{expectedFrameworkName}/enabled");
        }

        private void CountsAndValuesAreAsExpected()
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
                Assert.Equal(29, logLines.Length);

                foreach (var logLine in logLines)
                {
                    if (LogUtils.GetLevelName(_loggingFramework, "NOMESSAGE") == logLine.Level &&
                        (logLine.ErrorMessage.EndsWith("NoMessage") || logLine.ErrorMessage.StartsWith("Exception of type"))) // SerilogWeb uses MEL (built in) and that handles exception messages differently
                    {
                        Assert.True(string.IsNullOrWhiteSpace(logLine.Message));
                    }
                    else
                    {
                        Assert.False(string.IsNullOrWhiteSpace(logLine.Message));
                    }

                    Assert.False(string.IsNullOrWhiteSpace(logLine.Level));
                    Assert.NotEqual(0, logLine.Timestamp);

                    if (logLine.Level == LogUtils.GetLevelName(_loggingFramework, "ERROR"))
                    {
                        Assert.False(string.IsNullOrWhiteSpace(logLine.ErrorStack));
                        Assert.False(string.IsNullOrWhiteSpace(logLine.ErrorMessage));
                        Assert.False(string.IsNullOrWhiteSpace(logLine.ErrorClass));
                    }
                }
            }
            else
            {
                Assert.Empty(logLines);
            }
        }

        private void LoggingWorksWithTraceAttributeOutsideTransaction()
        {
            if (_forwardingEnabled && _canHaveLogsOutsideTransaction)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = TraceAttributeOutsideTransactionLogMessage, HasTraceId = false, HasSpanId = false, HasException = false }
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x => !string.IsNullOrWhiteSpace(x.Message) && x.Message.StartsWith("TraceAttributeOutsideTransaction")).Count());
            }
        }

        private void LoggingWorksWithDifferentTraceAttributesInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = DifferentTraceAttributesInsideTransactionLogMessage, HasTraceId = true, HasSpanId = true, HasException = false }
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                var logsOfInterest = logLines.Where(x => !string.IsNullOrWhiteSpace(x.Message) && x.Message.StartsWith("DifferentTraceAttributesInsideTransaction")).ToArray();

                Assert.Equal(2, logsOfInterest.Length);
                Assert.NotEqual(logsOfInterest[0].Spanid, logsOfInterest[1].Spanid);
            }
        }

        private void LoggingWorksInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                Assertions.ExpectedLogLine inTransactionExpectedLogLine;
                Assertions.ExpectedLogLine OutsideTransactionExpectedLogLine;
                if (_canHaveLogsOutsideTransaction)
                {
                    inTransactionExpectedLogLine = new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "NOMESSAGE"), LogMessage = null, HasTraceId = true, HasSpanId = true, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = _loggingFramework == LoggingFramework.SerilogWeb ? SerilogWebErrorNoMessage : InTransactionErrorNoMessage, ErrorClass = ErrorClassValue };
                    OutsideTransactionExpectedLogLine = new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "NOMESSAGE"), LogMessage = null, HasTraceId = false, HasSpanId = false, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = _loggingFramework == LoggingFramework.SerilogWeb ? SerilogWebErrorNoMessage : OutsideTransactionErrorNoMessage, ErrorClass = ErrorClassValue };
                }
                else
                {
                    // Serilog always exists in a transaction
                    inTransactionExpectedLogLine = new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "NOMESSAGE"), LogMessage = null, HasTraceId = true, HasSpanId = true, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = _loggingFramework == LoggingFramework.SerilogWeb ? SerilogWebErrorNoMessage : InTransactionErrorNoMessage, ErrorClass = ErrorClassValue };
                    OutsideTransactionExpectedLogLine = new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "NOMESSAGE"), LogMessage = null, HasTraceId = true, HasSpanId = true, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = _loggingFramework == LoggingFramework.SerilogWeb ? SerilogWebErrorNoMessage : OutsideTransactionErrorNoMessage, ErrorClass = ErrorClassValue };
                }

                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = InTransactionInfoMessage, HasTraceId = true, HasSpanId = true, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "ERROR"), LogMessage = InTransactionErrorMessage, HasTraceId = true, HasSpanId = true, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = InTransactionErrorMessage, ErrorClass = ErrorClassValue },

                    // 2 expected NOMESSAGE log lines since there is no way to tell the inside transaction line from the outside transaction line
                    // One line needs to have HasTraceId and HasSpanId set to false
                    inTransactionExpectedLogLine,
                    OutsideTransactionExpectedLogLine,
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x =>
                    (!string.IsNullOrWhiteSpace(x.Message) && x.Message.StartsWith("InTransaction"))
                    || string.IsNullOrWhiteSpace(x.Message)
                ).Count());
            }
        }

        private void AsyncLoggingWorksInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncInTransactionDebugMessage, HasTraceId = true, HasSpanId = true, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = AsyncInTransactionInfoMessage, HasTraceId = true, HasSpanId = true, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "WARN"), LogMessage = AsyncInTransactionWarningMessage, HasTraceId = true, HasSpanId = true, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncInTransactionErrorMessage, HasTraceId = true, HasSpanId = true, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = AsyncInTransactionErrorMessage, ErrorClass = ErrorClassValue },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Message) && x.Message.StartsWith("AsyncInTransaction")
                ).Count());
            }
        }

        private void AsyncNoAwaitLoggingWorksInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                // NOTE: since the log is not awaited, the logs intermittently show up in/out of a transaction.
                // Because of this the spanId/traceId members are not checked by not specifying true/false in the ExpectedLogLines
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncNoAwaitInTransactionDebugMessage, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = AsyncNoAwaitInTransactionInfoMessage, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "WARN"), LogMessage = AsyncNoAwaitInTransactionWarningMessage, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncNoAwaitInTransactionErrorMessage, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = AsyncNoAwaitInTransactionErrorMessage, ErrorClass = ErrorClassValue },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Message) && x.Message.StartsWith("AsyncNoAwaitInTransaction")
                ).Count());
            }
        }

        private void LoggingWorksOutsideTransaction()
        {
            if (_forwardingEnabled && _canHaveLogsOutsideTransaction)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = OutsideTransactionInfoMessage, HasSpanId = false, HasTraceId = false, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "ERROR"), LogMessage = OutsideTransactionErrorMessage, HasSpanId = false, HasTraceId = false, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = OutsideTransactionErrorMessage, ErrorClass = ErrorClassValue },

                    // 2 expected NOMESSAGE log lines since there is no way to tell the inside transaction line from the outside transaction line
                    // One line needs to have HasTraceId and HasSpanId set to true
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "NOMESSAGE"), LogMessage = null, HasSpanId = true, HasTraceId = true, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = _loggingFramework == LoggingFramework.SerilogWeb ? SerilogWebErrorNoMessage : InTransactionErrorNoMessage, ErrorClass = ErrorClassValue },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "NOMESSAGE"), LogMessage = null, HasSpanId = false, HasTraceId = false, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = _loggingFramework == LoggingFramework.SerilogWeb ? SerilogWebErrorNoMessage : OutsideTransactionErrorNoMessage, ErrorClass = ErrorClassValue },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x =>
                    (!string.IsNullOrWhiteSpace(x.Message) && x.Message.StartsWith("OutsideTransaction"))
                    || string.IsNullOrWhiteSpace(x.Message)
                ).Count());
            }
        }

        private void AsyncLoggingWorksOutsideTransaction()
        {
            if (_forwardingEnabled && _canHaveLogsOutsideTransaction)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncOutsideTransactionDebugMessage, HasSpanId = false, HasTraceId = false, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = AsyncOutsideTransactionInfoMessage, HasSpanId = false, HasTraceId = false, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "WARN"), LogMessage = AsyncOutsideTransactionWarningMessage, HasSpanId = false, HasTraceId = false, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncOutsideTransactionErrorMessage, HasSpanId = false, HasTraceId = false, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = AsyncOutsideTransactionErrorMessage, ErrorClass = ErrorClassValue },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Message) && x.Message.StartsWith("AsyncOutsideTransaction")
                ).Count());
            }
        }

        private void AsyncNoAwaitLoggingWorksOutsideTransaction()
        {
            if (_forwardingEnabled && _canHaveLogsOutsideTransaction)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncNoAwaitOutsideTransactionDebugMessage, HasSpanId = false, HasTraceId = false, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = AsyncNoAwaitOutsideTransactionInfoMessage, HasSpanId = false, HasTraceId = false, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "WARN"), LogMessage = AsyncNoAwaitOutsideTransactionWarningMessage, HasSpanId = false, HasTraceId = false, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncNoAwaitOutsideTransactionErrorMessage, HasSpanId = false, HasTraceId = false, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = AsyncNoAwaitOutsideTransactionErrorMessage, ErrorClass = ErrorClassValue },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Message) && x.Message.StartsWith("AsyncNoAwaitOutsideTransaction")
                ).Count());
            }
        }

        private void AsyncNoAwaitWithDelayLoggingWorksInsideTransaction()
        {
            if (_forwardingEnabled)
            {
                var expectedLogLines = new Assertions.ExpectedLogLine[]
                {
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "DEBUG"), LogMessage = AsyncNoAwaitWithDelayInTransactionDebugMessage, HasTraceId = true, HasSpanId = true, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = AsyncNoAwaitWithDelayInTransactionInfoMessage, HasTraceId = true, HasSpanId = true, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "WARN"), LogMessage = AsyncNoAwaitWithDelayInTransactionWarningMessage, HasTraceId = true, HasSpanId = true, HasException = false },
                    new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "ERROR"), LogMessage = AsyncNoAwaitWithDelayInTransactionErrorMessage, HasTraceId = true, HasSpanId = true, HasException = true, ErrorStack = ErrorStackValue, ErrorMessage = AsyncNoAwaitWithDelayInTransactionErrorMessage, ErrorClass = ErrorClassValue },
                };

                var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

                Assertions.LogLinesExist(expectedLogLines, logLines);

                Assert.Equal(expectedLogLines.Length, logLines.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Message) && x.Message.StartsWith("AsyncNoAwaitWithDelayInTransaction")
                ).Count());
            }
        }
    }

    #region log4net

    namespace log4net
    {
        #region Metrics and Forwarding Enabled
        [NetFrameworkTest]
        public class Log4NetMetricsAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
        {
            public Log4NetMetricsAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetFrameworkTest]
        public class Log4NetMetricsAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
        {
            public Log4NetMetricsAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetFrameworkTest]
        public class Log4NetMetricsAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
        {
            public Log4NetMetricsAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4NetMetricsAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
        {
            public Log4NetMetricsAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4NetMetricsAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
        {
            public Log4NetMetricsAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4NetMetricsAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
        {
            public Log4NetMetricsAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Log4net)
            {
            }
        }

        #endregion

        #region Metrics and Forwarding Disabled
        [NetFrameworkTest]
        public class Log4NetMetricsAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
        {
            public Log4NetMetricsAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetFrameworkTest]
        public class Log4NetMetricsAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
        {
            public Log4NetMetricsAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetFrameworkTest]
        public class Log4NetMetricsAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
        {
            public Log4NetMetricsAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4NetMetricsAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
        {
            public Log4NetMetricsAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4NetMetricsAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
        {
            public Log4NetMetricsAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4NetMetricsAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
        {
            public Log4NetMetricsAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Log4net)
            {
            }
        }

        #endregion

        #region Metrics Enabled, Forwarding Disabled
        [NetFrameworkTest]
        public class Log4NetMetricsEnabledAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
        {
            public Log4NetMetricsEnabledAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetFrameworkTest]
        public class Log4NetMetricsEnabledAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
        {
            public Log4NetMetricsEnabledAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetFrameworkTest]
        public class Log4NetMetricsEnabledAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
        {
            public Log4NetMetricsEnabledAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
        {
            public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
        {
            public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
        {
            public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Log4net)
            {
            }
        }

        #endregion

        #region Metrics Disabled, Forwarding Enabled
        [NetFrameworkTest]
        public class Log4netMetricsDisabledAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
        {
            public Log4netMetricsDisabledAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetFrameworkTest]
        public class Log4netMetricsDisabledAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
        {
            public Log4netMetricsDisabledAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetFrameworkTest]
        public class Log4netMetricsDisabledAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
        {
            public Log4netMetricsDisabledAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
        {
            public Log4netMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
        {
            public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Log4net)
            {
            }
        }

        [NetCoreTest]
        public class Log4netMetricsDisabledAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
        {
            public Log4netMetricsDisabledAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Log4net)
            {
            }
        }

        #endregion
    }

    #endregion

    #region MicrosoftLogging

    namespace MicrosoftLogging
    {
        #region Metrics and Forwarding Enabled

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore50Tests(
                ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public MicrosoftLoggingMetricsAndForwardingEnabledTestsNetCore31Tests(
                ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetFrameworkTest]
        public class
        MicrosoftLoggingMetricsAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
        ConsoleDynamicMethodFixtureFWLatest>
        {
            public MicrosoftLoggingMetricsAndForwardingEnabledTestsFWLatestTests(
                ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        #endregion

        #region Metrics and Forwarding Disabled

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore50Tests(
                ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public MicrosoftLoggingMetricsAndForwardingDisabledTestsNetCore31Tests(
                ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetFrameworkTest]
        public class
            MicrosoftLoggingMetricsAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
        ConsoleDynamicMethodFixtureFWLatest>
        {
            public MicrosoftLoggingMetricsAndForwardingDisabledTestsFWLatestTests(
                ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        #endregion

        #region Metrics Enabled, Forwarding Disabled

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore50Tests(
                ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsNetCore31Tests(
                ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetFrameworkTest]
        public class
            MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
        ConsoleDynamicMethodFixtureFWLatest>
        {
            public MicrosoftLoggingMetricsEnabledAndForwardingDisabledTestsFWLatestTests(
                ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        #endregion

        #region Metrics Disabled, Forwarding Enabled

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore50Tests(
                ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetCoreTest]
        public class
            MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsNetCore31Tests(
                ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        [NetFrameworkTest]
        public class
            MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFWLatest>
        {
            public MicrosoftLoggingMetricsDisabledAndForwardingEnabledTestsFWLatestTests(
                ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.MicrosoftLogging)
            {
            }
        }

        #endregion
    }

    #endregion

    #region Serilog

    namespace Serilog
    {
        #region Metrics and Forwarding Enabled

        [NetFrameworkTest]
        public class
            SerilogMetricsAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFWLatest>
        {
            public SerilogMetricsAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            SerilogMetricsAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW471>
        {
            public SerilogMetricsAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            SerilogMetricsAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW462>
        {
            public SerilogMetricsAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public SerilogMetricsAndForwardingEnabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public SerilogMetricsAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public SerilogMetricsAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogWebMetricsAndForwardingEnabledTestsNetCore60Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore60>
        {
            public SerilogWebMetricsAndForwardingEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, false, LoggingFramework.SerilogWeb)
            {
            }
        }

        #endregion

        #region Metrics and Forwarding Disabled

        [NetFrameworkTest]
        public class
            SerilogMetricsAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFWLatest>
        {
            public SerilogMetricsAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            SerilogMetricsAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW471>
        {
            public SerilogMetricsAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            SerilogMetricsAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW462>
        {
            public SerilogMetricsAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public SerilogMetricsAndForwardingDisabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public SerilogMetricsAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public SerilogMetricsAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.Serilog)
            {
            }
        }

        #endregion

        #region Metrics Enabled, Forwarding Disabled

        [NetFrameworkTest]
        public class
            SerilogMetricsEnabledAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFWLatest>
        {
            public SerilogMetricsEnabledAndForwardingDisabledTestsFWLatestTests(
                ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            SerilogMetricsEnabledAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW471>
        {
            public SerilogMetricsEnabledAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            SerilogMetricsEnabledAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW462>
        {
            public SerilogMetricsEnabledAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public SerilogMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsEnabledAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public SerilogMetricsEnabledAndForwardingDisabledTestsNetCore50Tests(
                ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsEnabledAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public SerilogMetricsEnabledAndForwardingDisabledTestsNetCore31Tests(
                ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.Serilog)
            {
            }
        }

        #endregion

        #region Metrics Disabled, Forwarding Enabled

        [NetFrameworkTest]
        public class
            SerilogMetricsDisabledAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFWLatest>
        {
            public SerilogMetricsDisabledAndForwardingEnabledTestsFWLatestTests(
                ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            SerilogMetricsDisabledAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW471>
        {
            public SerilogMetricsDisabledAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            SerilogMetricsDisabledAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW462>
        {
            public SerilogMetricsDisabledAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public SerilogMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsDisabledAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public SerilogMetricsDisabledAndForwardingEnabledTestsNetCore50Tests(
                ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Serilog)
            {
            }
        }

        [NetCoreTest]
        public class
            SerilogMetricsDisabledAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public SerilogMetricsDisabledAndForwardingEnabledTestsNetCore31Tests(
                ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.Serilog)
            {
            }
        }

        #endregion
    }

    #endregion

    #region NLog

    namespace NLog
    {
        #region Metrics and Forwarding Enabled

        [NetFrameworkTest]
        public class
            NLogMetricsAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFWLatest>
        {
            public NLogMetricsAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            NLogMetricsAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW471>
        {
            public NLogMetricsAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            NLogMetricsAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW462>
        {
            public NLogMetricsAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public NLogMetricsAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public NLogMetricsAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public NLogMetricsAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.NLog)
            {
            }
        }

        #endregion

        #region Metrics and Forwarding Disabled

        [NetFrameworkTest]
        public class
            NLogMetricsAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFWLatest>
        {
            public NLogMetricsAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            NLogMetricsAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW471>
        {
            public NLogMetricsAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            NLogMetricsAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW462>
        {
            public NLogMetricsAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public NLogMetricsAndForwardingDisabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public NLogMetricsAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public NLogMetricsAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, false, true, LoggingFramework.NLog)
            {
            }
        }

        #endregion

        #region Metrics Enabled, Forwarding Disabled

        [NetFrameworkTest]
        public class
            NLogMetricsEnabledAndForwardingDisabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFWLatest>
        {
            public NLogMetricsEnabledAndForwardingDisabledTestsFWLatestTests(
                ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            NLogMetricsEnabledAndForwardingDisabledTestsFW471Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW471>
        {
            public NLogMetricsEnabledAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            NLogMetricsEnabledAndForwardingDisabledTestsFW462Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW462>
        {
            public NLogMetricsEnabledAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public NLogMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsEnabledAndForwardingDisabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public NLogMetricsEnabledAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsEnabledAndForwardingDisabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public NLogMetricsEnabledAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture,
                ITestOutputHelper output)
                : base(fixture, output, true, false, true, LoggingFramework.NLog)
            {
            }
        }

        #endregion

        #region Metrics Disabled, Forwarding Enabled

        [NetFrameworkTest]
        public class
            NLogMetricsDisabledAndForwardingEnabledTestsFWLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFWLatest>
        {
            public NLogMetricsDisabledAndForwardingEnabledTestsFWLatestTests(
                ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            NLogMetricsDisabledAndForwardingEnabledTestsFW471Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW471>
        {
            public NLogMetricsDisabledAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetFrameworkTest]
        public class
            NLogMetricsDisabledAndForwardingEnabledTestsFW462Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureFW462>
        {
            public NLogMetricsDisabledAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCoreLatest>
        {
            public NLogMetricsDisabledAndForwardingEnabledTestsNetCoreLatestTests(
                ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsDisabledAndForwardingEnabledTestsNetCore50Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore50>
        {
            public NLogMetricsDisabledAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.NLog)
            {
            }
        }

        [NetCoreTest]
        public class
            NLogMetricsDisabledAndForwardingEnabledTestsNetCore31Tests : MetricsAndForwardingTestsBase<
                ConsoleDynamicMethodFixtureCore31>
        {
            public NLogMetricsDisabledAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture,
                ITestOutputHelper output)
                : base(fixture, output, false, true, true, LoggingFramework.NLog)
            {
            }
        }

        #endregion
    }

    #endregion

}
