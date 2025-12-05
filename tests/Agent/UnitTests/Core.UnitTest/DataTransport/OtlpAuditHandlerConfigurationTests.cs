// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Core.Logging;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTests.DataTransport
{
    [TestFixture]
    public class OtlpAuditHandlerConfigurationTests
    {
        [SetUp]
        public void SetUp()
        {
            // Reset audit log for testing
            AuditLog.ResetLazyLogger();
        }

        [TearDown]
        public void TearDown()
        {
            AuditLog.IsAuditLogEnabled = false;
        }

        [Test]
        public void AuditLog_WhenDisabled_IsAuditLogEnabledReturnsFalse()
        {
            // Arrange
            AuditLog.IsAuditLogEnabled = false;
            
            // Act & Assert
            Assert.That(AuditLog.IsAuditLogEnabled, Is.False, 
                "AuditLog.IsAuditLogEnabled should return false when audit logging is disabled");
        }

        [Test]
        public void AuditLog_WhenEnabled_IsAuditLogEnabledReturnsTrue()
        {
            // Arrange
            AuditLog.IsAuditLogEnabled = true;
            
            // Act & Assert
            Assert.That(AuditLog.IsAuditLogEnabled, Is.True, 
                "AuditLog.IsAuditLogEnabled should return true when audit logging is enabled");
        }

        [Test]
        public void DataTransportAuditLogger_WhenDisabled_DoesNotThrowForSentData()
        {
            // Arrange
            AuditLog.IsAuditLogEnabled = false;
            
            // Act & Assert - This should be a no-op when audit log is disabled
            Assert.DoesNotThrow(() => DataTransportAuditLogger.Log(
                DataTransportAuditLogger.AuditLogDirection.Sent,
                DataTransportAuditLogger.AuditLogSource.InstrumentedApp,
                "https://test.example.com"
            ), "DataTransportAuditLogger.Log should not throw when audit logging is disabled for sent data");
        }

        [Test]
        public void DataTransportAuditLogger_WhenDisabled_DoesNotThrowForReceivedData()
        {
            // Arrange
            AuditLog.IsAuditLogEnabled = false;
            
            // Act & Assert - This should be a no-op when audit log is disabled
            Assert.DoesNotThrow(() => DataTransportAuditLogger.Log(
                DataTransportAuditLogger.AuditLogDirection.Received,
                DataTransportAuditLogger.AuditLogSource.Collector,
                "Response: 200 (OK) from https://test.example.com"
            ), "DataTransportAuditLogger.Log should not throw when audit logging is disabled for received data");
        }

        [Test]
        public void DataTransportAuditLogger_WhenEnabled_DoesNotThrowForSentData()
        {
            // Arrange
            AuditLog.IsAuditLogEnabled = true;
            
            // Act & Assert - This should perform logging when audit log is enabled
            Assert.DoesNotThrow(() => DataTransportAuditLogger.Log(
                DataTransportAuditLogger.AuditLogDirection.Sent,
                DataTransportAuditLogger.AuditLogSource.InstrumentedApp,
                "https://otlp.nr-data.net/v1/metrics"
            ), "DataTransportAuditLogger.Log should not throw when audit logging is enabled for sent data");
        }

        [Test]
        public void DataTransportAuditLogger_WhenEnabled_DoesNotThrowForReceivedData()
        {
            // Arrange
            AuditLog.IsAuditLogEnabled = true;
            
            // Act & Assert - This should perform logging when audit log is enabled
            Assert.DoesNotThrow(() => DataTransportAuditLogger.Log(
                DataTransportAuditLogger.AuditLogDirection.Received,
                DataTransportAuditLogger.AuditLogSource.Collector,
                "Response: 200 (OK) for https://otlp.nr-data.net/v1/metrics"
            ), "DataTransportAuditLogger.Log should not throw when audit logging is enabled for received data");
        }
    }
}