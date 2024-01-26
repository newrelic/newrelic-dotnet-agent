// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Serilog;
using Serilog.Events;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Logging.Tests
{
    [TestFixture]
    public class LoggerTests
    {
        private Logger _logger;
        private Serilog.ILogger _serilogLogger;
        private string _testMessage;
        private Exception _testException;
        private string _testFormat;
        private object[] _testArgs;

        [SetUp]
        public void SetUp()
        {
            _serilogLogger = Mock.Create<Serilog.ILogger>();
            Log.Logger = _serilogLogger;
            _logger = new Logger();

            _testMessage = "Test message";
            _testException = new Exception("Test exception");
            _testFormat = "Test format {0}";
            _testArgs = new object[] { "arg1" };
        }

        [Test]
        [TestCase(Level.Finest, LogEventLevel.Verbose)]
        [TestCase(Level.Debug, LogEventLevel.Debug)]
        [TestCase(Level.Info, LogEventLevel.Information)]
        [TestCase(Level.Warn, LogEventLevel.Warning)]
        [TestCase(Level.Error, LogEventLevel.Error)]
        public void IsEnabledFor_Level_ReturnsCorrectValue(Level level, LogEventLevel logEventLevel)
        {
            Mock.Arrange(() => _serilogLogger.IsEnabled(logEventLevel)).Returns(true);

            var result = _logger.IsEnabledFor(level);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsEnabledFor_UnsupportedLevel_ReturnsFalse()
        {
            var result = _logger.IsEnabledFor((Level)9999);

            Assert.That(result, Is.False);
        }

        [Test]
        [TestCase(Level.Finest, LogEventLevel.Verbose)]
        [TestCase(Level.Debug, LogEventLevel.Debug)]
        [TestCase(Level.Info, LogEventLevel.Information)]
        [TestCase(Level.Warn, LogEventLevel.Warning)]
        [TestCase(Level.Error, LogEventLevel.Error)]
        public void Log_ValidLevel_CallsSerilogLogger(Level level, LogEventLevel logEventLevel)
        {
            string message = "Test message";
            Mock.Arrange(() => _serilogLogger.IsEnabled(logEventLevel)).Returns(true);
            _logger.Log(level, message);

            switch (level)
            {
                case Level.Finest:
                    Mock.Assert(() => _serilogLogger.Verbose(message), Occurs.Once());
                    break;
                case Level.Debug:
                    Mock.Assert(() => _serilogLogger.Debug(message), Occurs.Once());
                    break;
                case Level.Info:
                    Mock.Assert(() => _serilogLogger.Information(message), Occurs.Once());
                    break;
                case Level.Warn:
                    Mock.Assert(() => _serilogLogger.Warning(message), Occurs.Once());
                    break;
                case Level.Error:
                    Mock.Assert(() => _serilogLogger.Error(message), Occurs.Once());
                    break;
            }
        }

        [Test]
        public void Log_UnsupportedLevel_NoSerilogCalls()
        {
            _logger.Log((Level)9999, _testMessage);
            Mock.Assert(() => _serilogLogger.Verbose(Arg.AnyString), Occurs.Never());
            Mock.Assert(() => _serilogLogger.Debug(Arg.AnyString), Occurs.Never());
            Mock.Assert(() => _serilogLogger.Information(Arg.AnyString), Occurs.Never());
            Mock.Assert(() => _serilogLogger.Warning(Arg.AnyString), Occurs.Never());
            Mock.Assert(() => _serilogLogger.Error(Arg.AnyString), Occurs.Never());
        }


        [Test]
        public void IsErrorEnabled_ReturnsCorrectValue()
        {
            Mock.Arrange(() => _serilogLogger.IsEnabled(LogEventLevel.Error)).Returns(true);

            var result = _logger.IsErrorEnabled;

            Assert.That(result, Is.True);
        }

        [Test]
        public void Error_LogsError()
        {
            string message = "Test Error";
            _logger.Error(message);

            Mock.Assert(() => _serilogLogger.Error(message, Arg.IsAny<object[]>()), Occurs.Once());
        }

        [Test]
        public void Error_Exception_LogsError()
        {
            var exception = new Exception("Test Exception");
            string message = "Test Error";
            _logger.Error(exception, message);

            Mock.Assert(() => _serilogLogger.Error(exception, message, Arg.IsAny<object[]>()), Occurs.Once());
        }

        // Level methods

        [Test]
        public void IsEnabledFor_ShouldCallSerilogLoggerIsEnabled()
        {
            _logger.IsEnabledFor(Level.Debug);
            Mock.Assert(() => _serilogLogger.IsEnabled(Arg.IsAny<LogEventLevel>()), Occurs.Once());
        }

        // Error methods

        [Test]
        public void Error_Message_CallsSerilogLoggerError()
        {
            _logger.Error(_testMessage);
            Mock.Assert(() => _serilogLogger.Error(_testMessage, Arg.IsAny<object[]>()), Occurs.Once());
        }

        [Test]
        public void Error_Exception_CallsSerilogLoggerError()
        {
            _logger.Error(_testException, "");
            Mock.Assert(() => _serilogLogger.Error(_testException, Arg.AnyString, Arg.IsAny<object[]>()), Occurs.Once());
        }

        [Test]
        public void ErrorFormat_CallsSerilogLoggerError()
        {
            Mock.Arrange(() => _serilogLogger.IsEnabled(Arg.IsAny<LogEventLevel>())).Returns(true);

            _logger.Error(_testFormat, _testArgs);
            Mock.Assert(() => _serilogLogger.Error(Arg.AnyString, Arg.IsAny<object[]>()), Occurs.Once());
        }

        // Warn methods

        [Test]
        public void Warn_Message_CallsSerilogLoggerWarning()
        {
            _logger.Warn(_testMessage);
            Mock.Assert(() => _serilogLogger.Warning(_testMessage, Arg.IsAny<object[]>()), Occurs.Once());
        }

        [Test]
        public void Warn_Exception_CallsSerilogLoggerWarning()
        {
            _logger.Warn(_testException, "");
            Mock.Assert(() => _serilogLogger.Warning(_testException, Arg.AnyString, Arg.IsAny<object[]>()), Occurs.Once());
        }

        [Test]
        public void WarnFormat_CallsSerilogLoggerWarning()
        {
            Mock.Arrange(() => _serilogLogger.IsEnabled(Arg.IsAny<LogEventLevel>())).Returns(true);

            _logger.Warn(_testFormat, _testArgs);
            Mock.Assert(() => _serilogLogger.Warning(Arg.AnyString, Arg.IsAny<object[]>()), Occurs.Once());
        }

        // Info methods

        [Test]
        public void Info_Message_CallsSerilogLoggerInformation()
        {
            _logger.Info(_testMessage);
            Mock.Assert(() => _serilogLogger.Information(_testMessage, Arg.IsAny<object[]>()), Occurs.Once());
        }

        [Test]
        public void Info_Exception_CallsSerilogLoggerInformation()
        {
            _logger.Info(_testException, "");
            Mock.Assert(() => _serilogLogger.Information(_testException, Arg.AnyString, Arg.IsAny<object[]>()), Occurs.Once());
        }

        [Test]
        public void InfoFormat_CallsSerilogLoggerInformation()
        {
            Mock.Arrange(() => _serilogLogger.IsEnabled(Arg.IsAny<LogEventLevel>())).Returns(true);

            _logger.Info(_testFormat, _testArgs);
            Mock.Assert(() => _serilogLogger.Information(Arg.AnyString, Arg.IsAny<object[]>()), Occurs.Once());
        }

        // Debug methods

        [Test]
        public void Debug_Message_CallsSerilogLoggerDebug()
        {
            _logger.Debug(_testMessage);
            Mock.Assert(() => _serilogLogger.Debug(_testMessage, Arg.IsAny<object[]>()), Occurs.Once());
        }

        [Test]
        public void Debug_Exception_CallsSerilogLoggerDebug()
        {
            _logger.Debug(_testException, "");
            Mock.Assert(() => _serilogLogger.Debug(_testException, Arg.AnyString, Arg.IsAny<object[]>()), Occurs.Once());
        }

        [Test]
        public void DebugFormat_CallsSerilogLoggerDebug()
        {
            Mock.Arrange(() => _serilogLogger.IsEnabled(Arg.IsAny<LogEventLevel>())).Returns(true);

            _logger.Debug(_testFormat, _testArgs);
            Mock.Assert(() => _serilogLogger.Debug(Arg.AnyString, Arg.IsAny<object[]>()), Occurs.Once());
        }
        [TearDown]
        public void TearDown()
        {
            _logger = null;
            _serilogLogger = null;
        }
    }
}
