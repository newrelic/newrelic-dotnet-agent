// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using Serilog;
using Serilog.Configuration;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Logging.Tests
{
    [TestFixture]
    public class AuditLogTests
    {
        private ILogger _mockILogger;

        [SetUp]
        public void SetUp()
        {
            _mockILogger = Mock.Create<ILogger>();
            Log.Logger = _mockILogger;

            // reset state for each test
            AuditLog.IsAuditLogEnabled = false;
        }

        [Test]
        public void IncludeOnlyAuditLog_EnablesAuditLog()
        {
            Assert.False(AuditLog.IsAuditLogEnabled);

            var _ = new LoggerConfiguration().IncludeOnlyAuditLog();

            Assert.True(AuditLog.IsAuditLogEnabled);
        }

        [Test]
        public void ExcludeAuditLog_DisablesAuditLog()
        {
            AuditLog.IsAuditLogEnabled = true;

            var _ = new LoggerConfiguration().ExcludeAuditLog();

            Assert.False(AuditLog.IsAuditLogEnabled);
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void Log_OnlyLogsWhenAuditLogEnabled(bool logEnabled)
        {
            // ensure that .ForContext() just returns the mock logger instance
            Mock.Arrange(() => _mockILogger.ForContext(Arg.AnyString, Arg.AnyObject, false))
                .Returns(() => _mockILogger);
        
            AuditLog.IsAuditLogEnabled = logEnabled;

            var message = "This is an audit message";

            AuditLog.Log(message);

            Mock.Assert(() => _mockILogger.Fatal(message), logEnabled ? Occurs.Once() : Occurs.Never());
        }
    }
}
