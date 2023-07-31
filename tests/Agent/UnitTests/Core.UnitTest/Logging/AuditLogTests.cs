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
            AuditLog.ResetLazyLogger();
            AuditLog.IsAuditLogEnabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            AuditLog.ResetLazyLogger();
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

        [TestCase(true)]
        [TestCase(false)]
        public void Log_OnlyLogsWhenAuditLogEnabled(bool logEnabled)
        {
            var mockForContextLogger = Mock.Create<ILogger>();
            Mock.Arrange(() => _mockILogger.ForContext(Arg.AnyString, Arg.AnyObject, false))
                .Returns(() => mockForContextLogger);
        
            AuditLog.IsAuditLogEnabled = logEnabled;

            var message = "This is an audit message";

            AuditLog.Log(message);

            Mock.Assert(() => mockForContextLogger.Fatal(message), logEnabled ? Occurs.Once() : Occurs.Never());
        }
    }
}
