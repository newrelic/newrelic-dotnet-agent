// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;
using Serilog.Events;
using Telerik.JustMock;
using ILogger = Serilog.ILogger;

namespace NewRelic.Agent.Core.Logging.Tests
{
    [TestFixture]
    public class LogLevelExtensionsTests
    {
        private ILogger _serilogLogger;

        [SetUp]
        public void Setup()
        {
            _serilogLogger = Mock.Create<Serilog.ILogger>();
            Serilog.Log.Logger = _serilogLogger;
        }
        [Test]
        [TestCase("Alert", ExpectedResult = true)]
        [TestCase("Critical", ExpectedResult = true)]
        [TestCase("Emergency", ExpectedResult = true)]
        [TestCase("Fatal", ExpectedResult = true)]
        [TestCase("Finer", ExpectedResult = true)]
        [TestCase("Trace", ExpectedResult = true)]
        [TestCase("Notice", ExpectedResult = true)]
        [TestCase("Severe", ExpectedResult = true)]
        [TestCase("Verbose", ExpectedResult = true)]
        [TestCase("Fine", ExpectedResult = true)]
        [TestCase("Other", ExpectedResult = false)]
        [TestCase("", ExpectedResult = false)]
        [TestCase(null, ExpectedResult = false)]
        public bool IsLogLevelDeprecated_ReturnsCorrectResult(string logLevel)
        {
            return logLevel.IsLogLevelDeprecated();
        }

        [Test]
        [TestCase("Verbose", ExpectedResult = LogEventLevel.Verbose)]
        [TestCase("Fine", ExpectedResult = LogEventLevel.Verbose)]
        [TestCase("Finer", ExpectedResult = LogEventLevel.Verbose)]
        [TestCase("Finest", ExpectedResult = LogEventLevel.Verbose)]
        [TestCase("Trace", ExpectedResult = LogEventLevel.Verbose)]
        [TestCase("All", ExpectedResult = LogEventLevel.Verbose)]
        [TestCase("Debug", ExpectedResult = LogEventLevel.Debug)]
        [TestCase("Info", ExpectedResult = LogEventLevel.Information)]
        [TestCase("Notice", ExpectedResult = LogEventLevel.Information)]
        [TestCase("Warn", ExpectedResult = LogEventLevel.Warning)]
        [TestCase("Alert", ExpectedResult = LogEventLevel.Warning)]
        [TestCase("Error", ExpectedResult = LogEventLevel.Error)]
        [TestCase("Critical", ExpectedResult = LogEventLevel.Error)]
        [TestCase("Emergency", ExpectedResult = LogEventLevel.Error)]
        [TestCase("Fatal", ExpectedResult = LogEventLevel.Error)]
        [TestCase("Severe", ExpectedResult = LogEventLevel.Error)]
        [TestCase("Off", ExpectedResult = (LogEventLevel)6)]
        [TestCase(LogLevelExtensions.AuditLevel, ExpectedResult = LogEventLevel.Information)]
        [TestCase("NonExistent", ExpectedResult = LogEventLevel.Information)]
        public LogEventLevel MapToSerilogLogLevel_ReturnsCorrectResult(string configLogLevel)
        {
            return configLogLevel.MapToSerilogLogLevel();
        }

        [Test]
        public void MapToSerilogLogLevel_LogsDeprecationWarning_IfLogLevelIsDeprecated()
        {
            string deprecatedLogLevel = "Severe";
            deprecatedLogLevel.MapToSerilogLogLevel();

            Mock.Assert(() => _serilogLogger.Warning(Arg.AnyString), Occurs.Once());
        }

        [Test]
        public void MapToSerilogLogLevel_LogsWarning_IfLogLevelIsAudit()
        {
            string auditLevel = LogLevelExtensions.AuditLevel;
            auditLevel.MapToSerilogLogLevel();

            Mock.Assert(() => _serilogLogger.Warning(Arg.AnyString), Occurs.Once());
        }

        [Test]
        public void MapToSerilogLogLevel_LogsWarning_IfLogLevelIsInvalid()
        {
            string deprecatedLogLevel = "FOOBAR";
            deprecatedLogLevel.MapToSerilogLogLevel();

            Mock.Assert(() => _serilogLogger.Warning(Arg.AnyString), Occurs.Once());
        }

        [Test]
        [TestCase(LogEventLevel.Verbose, ExpectedResult = "FINEST")]
        [TestCase(LogEventLevel.Debug, ExpectedResult = "DEBUG")]
        [TestCase(LogEventLevel.Information, ExpectedResult = "INFO")]
        [TestCase(LogEventLevel.Warning, ExpectedResult = "WARN")]
        [TestCase(LogEventLevel.Error, ExpectedResult = "ERROR")]
        public string TranslateLogLevel_ReturnsCorrectResult(LogEventLevel logEventLevel)
        {
            return logEventLevel.TranslateLogLevel();
        }

        [Test]
        public void TranslateLogLevel_Throws_IfLogEventLevelIsInvalid()
        {
            var invalidLogLevel = (LogEventLevel)9999;

            Assert.Throws<ArgumentOutOfRangeException>(() => invalidLogLevel.TranslateLogLevel());
        }
    }
}
