// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTests.OpenTelemetryBridge
{
    [TestFixture]
    public class MeterBridgeConfigurationTests
    {
        [Test]
        public void BuildOtlpEndpoint_WithNullConnectionInfo_ReturnsNull()
        {
            var config = Mock.Create<IConfiguration>();
            var bridgeConfig = new MeterBridgeConfiguration(config);

            Assert.That(bridgeConfig.BuildOtlpEndpoint(null), Is.Null);
        }

        [Test]
        public void BuildOtlpEndpoint_WithHttps_ReturnsCorrectUri()
        {
            var config = Mock.Create<IConfiguration>();
            var connectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => connectionInfo.Host).Returns("collector.newrelic.com");
            Mock.Arrange(() => connectionInfo.Port).Returns(443);

            var bridgeConfig = new MeterBridgeConfiguration(config);
            var result = bridgeConfig.BuildOtlpEndpoint(connectionInfo);

            Assert.That(result.ToString(), Is.EqualTo("https://collector.newrelic.com/v1/metrics"));
        }

        [Test]
        public void BuildOtlpEndpoint_WithHttp_ReturnsCorrectUri()
        {
            var config = Mock.Create<IConfiguration>();
            var connectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => connectionInfo.HttpProtocol).Returns("http");
            Mock.Arrange(() => connectionInfo.Host).Returns("localhost");
            Mock.Arrange(() => connectionInfo.Port).Returns(4318);

            var bridgeConfig = new MeterBridgeConfiguration(config);
            var result = bridgeConfig.BuildOtlpEndpoint(connectionInfo);

            Assert.That(result.ToString(), Is.EqualTo("http://localhost:4318/v1/metrics"));
        }

        [Test]
        public void IsMetricsEnabled_WhenTrue_ReturnsTrue()
        {
            var config = Mock.Create<IConfiguration>();
            Mock.Arrange(() => config.OpenTelemetryMetricsEnabled).Returns(true);

            var bridgeConfig = new MeterBridgeConfiguration(config);

            Assert.That(bridgeConfig.IsMetricsEnabled(), Is.True);
        }

        [Test]
        public void IsMetricsEnabled_WhenFalse_ReturnsFalse()
        {
            var config = Mock.Create<IConfiguration>();
            Mock.Arrange(() => config.OpenTelemetryMetricsEnabled).Returns(false);

            var bridgeConfig = new MeterBridgeConfiguration(config);

            Assert.That(bridgeConfig.IsMetricsEnabled(), Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_DelegatesToFilterService()
        {
            var config = Mock.Create<IConfiguration>();
            Mock.Arrange(() => config.OpenTelemetryMetricsIncludeFilters).Returns(new[] { "IncludedMeter" });
            Mock.Arrange(() => config.OpenTelemetryMetricsExcludeFilters).Returns(new[] { "ExcludedMeter" });

            var bridgeConfig = new MeterBridgeConfiguration(config);

            Assert.That(bridgeConfig.ShouldEnableInstrumentsInMeter("IncludedMeter"), Is.True);
            Assert.That(bridgeConfig.ShouldEnableInstrumentsInMeter("ExcludedMeter"), Is.False);
            Assert.That(bridgeConfig.ShouldEnableInstrumentsInMeter("CustomMeter"), Is.True);
        }
    }
}
