// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTests.OpenTelemetryBridge
{
    [TestFixture]
    public class MeterFilterServiceTests
    {
        [Test]
        public void ShouldEnableInstrumentsInMeter_WithNullMeterName_ReturnsFalse()
        {
            var config = Mock.Create<IConfiguration>();
            var service = new MeterFilterService(config);

            Assert.That(service.ShouldEnableInstrumentsInMeter(null), Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_WithEmptyMeterName_ReturnsFalse()
        {
            var config = Mock.Create<IConfiguration>();
            var service = new MeterFilterService(config);

            Assert.That(service.ShouldEnableInstrumentsInMeter(""), Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_ExcludeListTakesPrecedence()
        {
            var config = Mock.Create<IConfiguration>();
            Mock.Arrange(() => config.OpenTelemetryMetricsIncludeFilters).Returns(new[] { "TestMeter" });
            Mock.Arrange(() => config.OpenTelemetryMetricsExcludeFilters).Returns(new[] { "TestMeter" });
            var service = new MeterFilterService(config);

            Assert.That(service.ShouldEnableInstrumentsInMeter("TestMeter"), Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_IncludeListOverridesBuiltInExclusions()
        {
            var config = Mock.Create<IConfiguration>();
            Mock.Arrange(() => config.OpenTelemetryMetricsIncludeFilters).Returns(new[] { "NewRelic.Test" });
            Mock.Arrange(() => config.OpenTelemetryMetricsExcludeFilters).Returns(new string[0]);
            var service = new MeterFilterService(config);

            Assert.That(service.ShouldEnableInstrumentsInMeter("NewRelic.Test"), Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_BuiltInExclusionsBlocked()
        {
            var config = Mock.Create<IConfiguration>();
            Mock.Arrange(() => config.OpenTelemetryMetricsIncludeFilters).Returns(new string[0]);
            Mock.Arrange(() => config.OpenTelemetryMetricsExcludeFilters).Returns(new string[0]);
            var service = new MeterFilterService(config);

            Assert.That(service.ShouldEnableInstrumentsInMeter("NewRelic.Agent"), Is.False);
            Assert.That(service.ShouldEnableInstrumentsInMeter("OpenTelemetry.Test"), Is.False);
            Assert.That(service.ShouldEnableInstrumentsInMeter("System.Diagnostics.Metrics.Test"), Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_DefaultPermissive()
        {
            var config = Mock.Create<IConfiguration>();
            Mock.Arrange(() => config.OpenTelemetryMetricsIncludeFilters).Returns(new string[0]);
            Mock.Arrange(() => config.OpenTelemetryMetricsExcludeFilters).Returns(new string[0]);
            var service = new MeterFilterService(config);

            Assert.That(service.ShouldEnableInstrumentsInMeter("CustomMeter"), Is.True);
        }

        [Test]
        public void IsNotBuiltInExclusion_WithNull_ReturnsFalse()
        {
            Assert.That(MeterFilterService.IsNotBuiltInExclusion(null), Is.False);
        }

        [Test]
        public void IsNotBuiltInExclusion_WithEmpty_ReturnsFalse()
        {
            Assert.That(MeterFilterService.IsNotBuiltInExclusion(""), Is.False);
        }

        [Test]
        public void IsNotBuiltInExclusion_WithNewRelicPrefix_ReturnsFalse()
        {
            Assert.That(MeterFilterService.IsNotBuiltInExclusion("NewRelic.Test"), Is.False);
            Assert.That(MeterFilterService.IsNotBuiltInExclusion("newrelic.test"), Is.False);
        }

        [Test]
        public void IsNotBuiltInExclusion_WithOpenTelemetryPrefix_ReturnsFalse()
        {
            Assert.That(MeterFilterService.IsNotBuiltInExclusion("OpenTelemetry.Test"), Is.False);
            Assert.That(MeterFilterService.IsNotBuiltInExclusion("opentelemetry.test"), Is.False);
        }

        [Test]
        public void IsNotBuiltInExclusion_WithSystemDiagnosticsMetricsPrefix_ReturnsFalse()
        {
            Assert.That(MeterFilterService.IsNotBuiltInExclusion("System.Diagnostics.Metrics.Test"), Is.False);
        }

        [Test]
        public void IsNotBuiltInExclusion_WithCustomMeter_ReturnsTrue()
        {
            Assert.That(MeterFilterService.IsNotBuiltInExclusion("CustomMeter"), Is.True);
            Assert.That(MeterFilterService.IsNotBuiltInExclusion("MyApp.Metrics"), Is.True);
        }
    }
}
