// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge
{
    [TestFixture]
    public class MeterFilterHelpersTests
    {
        private IConfiguration _configuration;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_PermissiveMode()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns(new List<string>());

            var result = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "AnyMeter");

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_ExcludeList()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns(new List<string> { "ExcludedMeter" });

            var excluded = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "ExcludedMeter");
            var allowed = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "AllowedMeter");

            Assert.That(excluded, Is.False);
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_IncludeOverridesBuiltIn()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns(new List<string> { "NewRelic.Test" });
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns(new List<string>());

            var result = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "NewRelic.Test");

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_BuiltInExclusions()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns(new List<string>());

            var newRelic = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "NewRelic.Test");
            var otel = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "OpenTelemetry.Test");

            Assert.That(newRelic, Is.False);
            Assert.That(otel, Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_NullFilters()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns((IEnumerable<string>)null);

            var result = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "TestMeter");

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_EmptyOrNullMeterName()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns((IEnumerable<string>)null);

            var nullResult = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, null);
            var emptyResult = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "");

            Assert.That(nullResult, Is.False);
            Assert.That(emptyResult, Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_FiltersDoNotContainMeter()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns(new List<string> { "IncludedMeter" });
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns(new List<string> { "ExcludedMeter" });

            var result = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "OtherMeter");

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_NullExcludeWithInclude()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns(new List<string> { "IncludedMeter" });
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns((IEnumerable<string>)null);

            var included = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "IncludedMeter");
            var other = MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configuration, "OtherMeter");

            Assert.That(included, Is.True);
            Assert.That(other, Is.True);
        }

        [Test]
        public void IsNotBuiltInExclusion_NullOrEmpty()
        {
            var nullResult = MeterFilterHelpers.IsNotBuiltInExclusion(null);
            var emptyResult = MeterFilterHelpers.IsNotBuiltInExclusion("");

            Assert.That(nullResult, Is.False);
            Assert.That(emptyResult, Is.False);
        }
    }
}
