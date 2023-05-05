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

        public MetricsAndForwardingTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework, bool canHaveLogsOutsideTransaction = true) : base(fixture)
        {
            _fixture = fixture;
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
            SupportabilityLoggingForwardingEnabledWithFrameworkMetricExists();
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
            Assertions.MetricsExist(loggingMetrics, actualMetrics);
        }

        private void SupportabilityForwardingConfigurationMetricExists()
        {
            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assert.Contains(actualMetrics, x => x.MetricSpec.Name == "Supportability/Logging/Forwarding/DotNET/enabled");
        }

        private void SupportabilityMetricsConfigurationMetricExists()
        {
            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assert.Contains(actualMetrics, x => x.MetricSpec.Name == "Supportability/Logging/Metrics/DotNET/enabled");
        }

        private void SupportabilityLoggingFrameworkMetricExists()
        {
            var expectedFrameworkName = LogUtils.GetFrameworkName(_loggingFramework);
            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assert.Contains(actualMetrics, x => x.MetricSpec.Name == $"Supportability/Logging/DotNET/{expectedFrameworkName}/enabled");
        }

        private void SupportabilityLoggingForwardingEnabledWithFrameworkMetricExists()
        {
            var expectedFrameworkName = LogUtils.GetFrameworkName(_loggingFramework);
            var actualMetrics = _fixture.AgentLog.GetMetrics();

            Assert.Contains(actualMetrics, x => x.MetricSpec.Name == $"Supportability/Logging/Forwarding/DotNET/{expectedFrameworkName}/enabled");
        }

        private void CountsAndValuesAreAsExpected()
        {
            var logEventData = _fixture.AgentLog.GetLogEventData().FirstOrDefault();
            Assert.NotNull(logEventData);
            Assert.NotNull(logEventData.Common);
            Assert.NotNull(logEventData.Common.Attributes);
            Assert.False(string.IsNullOrWhiteSpace(logEventData.Common.Attributes.EntityGuid));
            Assert.False(string.IsNullOrWhiteSpace(logEventData.Common.Attributes.EntityName));
            Assert.False(string.IsNullOrWhiteSpace(logEventData.Common.Attributes.Hostname));

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();
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

        private void LoggingWorksWithTraceAttributeOutsideTransaction()
        {
            if (_canHaveLogsOutsideTransaction)
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

        private void LoggingWorksInsideTransaction()
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

        private void AsyncLoggingWorksInsideTransaction()
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

        private void AsyncNoAwaitLoggingWorksInsideTransaction()
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

        private void LoggingWorksOutsideTransaction()
        {
            if (_canHaveLogsOutsideTransaction)
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
            if (_canHaveLogsOutsideTransaction)
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
            if (_canHaveLogsOutsideTransaction)
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

    #region log4net

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsAndForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingTestsFW471Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsAndForwardingTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingTestsFW462Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsAndForwardingTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsAndForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingTestsNetCore60Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4NetMetricsAndForwardingTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsAndForwardingTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsAndForwardingTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }
    #endregion

    #region MEL

    [NetCoreTest]
    public class MELMetricsAndForwardingTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MELMetricsAndForwardingTestsNetCoreLatestTests(
            ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MELMetricsAndForwardingTestsNetCore60Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MELMetricsAndForwardingTestsNetCore60Tests(
            ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MELMetricsAndForwardingTestsNetCore50Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MELMetricsAndForwardingTestsNetCore50Tests(
            ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class
        MELMetricsAndForwardingTestsNetCore31Tests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MELMetricsAndForwardingTestsNetCore31Tests(
            ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class
    MELMetricsAndForwardingTestsFWLatestTests : MetricsAndForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MELMetricsAndForwardingTestsFWLatestTests(
            ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class
        SerilogMetricsAndForwardingTestsFWLatestTests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogMetricsAndForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        SerilogMetricsAndForwardingTestsFW471Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogMetricsAndForwardingTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        SerilogMetricsAndForwardingTestsFW462Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogMetricsAndForwardingTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class
        SerilogMetricsAndForwardingTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogMetricsAndForwardingTestsNetCoreLatestTests(
            ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class
        SerilogMetricsAndForwardingTestsNetCore60Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogMetricsAndForwardingTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class
        SerilogMetricsAndForwardingTestsNetCore50Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogMetricsAndForwardingTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class
        SerilogMetricsAndForwardingTestsNetCore31Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogMetricsAndForwardingTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class
        SerilogWebMetricsAndForwardingTestsNetCore60Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogWebMetricsAndForwardingTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.SerilogWeb, canHaveLogsOutsideTransaction: false)
        {
        }
    }

    #endregion

    #region NLog

    [NetFrameworkTest]
    public class
        NLogMetricsAndForwardingTestsFWLatestTests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogMetricsAndForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        NLogMetricsAndForwardingTestsFW471Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureFW471>
    {
        public NLogMetricsAndForwardingTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class
        NLogMetricsAndForwardingTestsFW462Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureFW462>
    {
        public NLogMetricsAndForwardingTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class
        NLogMetricsAndForwardingTestsNetCoreLatestTests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogMetricsAndForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class
        NLogMetricsAndForwardingTestsNetCore60Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureCore60>
    {
        public NLogMetricsAndForwardingTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class
        NLogMetricsAndForwardingTestsNetCore50Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureCore50>
    {
        public NLogMetricsAndForwardingTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class
        NLogMetricsAndForwardingTestsNetCore31Tests : MetricsAndForwardingTestsBase<
            ConsoleDynamicMethodFixtureCore31>
    {
        public NLogMetricsAndForwardingTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    #endregion

}
