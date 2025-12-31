// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge
{
    [TestFixture]
    public class OtlpExporterConfigurationServiceTests
    {
        private IConfigurationService _mockConfigService;
        private IConfiguration _mockConfig;
        private IOtelBridgeSupportabilityMetricCounters _mockMetrics;
        private OtlpExporterConfigurationService _service;

        [SetUp]
        public void SetUp()
        {
            _mockConfigService = Mock.Create<IConfigurationService>();
            _mockConfig = Mock.Create<IConfiguration>();
            _mockMetrics = Mock.Create<IOtelBridgeSupportabilityMetricCounters>();
            var mockBridgeConfig = Mock.Create<MeterBridgeConfiguration>();

            Mock.Arrange(() => _mockConfigService.Configuration).Returns(_mockConfig);
            Mock.Arrange(() => _mockConfig.ApplicationNames).Returns(new[] { "TestApp" });
            Mock.Arrange(() => _mockConfig.AgentLicenseKey).Returns("test-license-key");
            Mock.Arrange(() => _mockConfig.EntityGuid).Returns("test-guid");
            Mock.Arrange(() => _mockConfig.OpenTelemetryOtlpExportIntervalSeconds).Returns(60);
            Mock.Arrange(() => _mockConfig.OpenTelemetryOtlpTimeoutSeconds).Returns(30);

            _service = new OtlpExporterConfigurationService(_mockConfigService, _mockMetrics, mockBridgeConfig);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
        }

        [Test]
        public void GetOrCreateMeterProvider_WithNullConnectionInfo_ReturnsNull()
        {
            // Act
            var result = _service.GetOrCreateMeterProvider(null, "test-guid");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetOrCreateMeterProvider_WithValidConnectionInfo_CreatesMeterProvider()
        {
            // Arrange
            var mockConnectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => mockConnectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => mockConnectionInfo.Host).Returns("collector.newrelic.com");
            Mock.Arrange(() => mockConnectionInfo.Port).Returns(443);
            Mock.Arrange(() => mockConnectionInfo.Proxy).Returns<System.Net.IWebProxy>(null);

            // Act
            var result = _service.GetOrCreateMeterProvider(mockConnectionInfo, "test-guid");

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void GetOrCreateMeterProvider_CalledTwiceWithSameParams_ReturnsSameInstance()
        {
            // Arrange
            var mockConnectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => mockConnectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => mockConnectionInfo.Host).Returns("collector.newrelic.com");
            Mock.Arrange(() => mockConnectionInfo.Port).Returns(443);
            Mock.Arrange(() => mockConnectionInfo.Proxy).Returns<System.Net.IWebProxy>(null);

            // Act
            var result1 = _service.GetOrCreateMeterProvider(mockConnectionInfo, "test-guid");
            var result2 = _service.GetOrCreateMeterProvider(mockConnectionInfo, "test-guid");

            // Assert
            Assert.That(result1, Is.SameAs(result2));
        }

        [Test]
        public void GetOrCreateMeterProvider_WithDifferentEntityGuid_RecreatesMeterProvider()
        {
            // Arrange
            var mockConnectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => mockConnectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => mockConnectionInfo.Host).Returns("collector.newrelic.com");
            Mock.Arrange(() => mockConnectionInfo.Port).Returns(443);
            Mock.Arrange(() => mockConnectionInfo.Proxy).Returns<System.Net.IWebProxy>(null);

            // Act
            var result1 = _service.GetOrCreateMeterProvider(mockConnectionInfo, "guid1");
            var result2 = _service.GetOrCreateMeterProvider(mockConnectionInfo, "guid2");

            // Assert
            Assert.That(result1, Is.Not.SameAs(result2));
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.AtLeastOnce());
        }

        [Test]
        public void RecreateMeterProvider_RecreatesMeterProvider()
        {
            // Arrange
            var mockConnectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => mockConnectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => mockConnectionInfo.Host).Returns("collector.newrelic.com");
            Mock.Arrange(() => mockConnectionInfo.Port).Returns(443);
            Mock.Arrange(() => mockConnectionInfo.Proxy).Returns<System.Net.IWebProxy>(null);

            var result1 = _service.GetOrCreateMeterProvider(mockConnectionInfo, "test-guid");

            // Act
            _service.RecreateMeterProvider();
            var result2 = _service.GetOrCreateMeterProvider();

            // Assert
            Assert.That(result1, Is.Not.SameAs(result2));
        }
    }
}
