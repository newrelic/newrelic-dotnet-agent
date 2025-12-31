// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge
{
    [TestFixture]
    public class MeterFilterServiceTests
    {
        private IConfiguration _configuration;
        private MeterFilterService _service;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            _service = new MeterFilterService();
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_PermissiveMode()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns(new List<string>());

            var result = _service.ShouldEnableInstrumentsInMeter(_configuration, "AnyMeter");

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_ExcludeList()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns(new List<string> { "ExcludedMeter" });

            var excluded = _service.ShouldEnableInstrumentsInMeter(_configuration, "ExcludedMeter");
            var allowed = _service.ShouldEnableInstrumentsInMeter(_configuration, "AllowedMeter");

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

            var result = _service.ShouldEnableInstrumentsInMeter(_configuration, "NewRelic.Test");

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_BuiltInExclusions()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns(new List<string>());

            var newRelic = _service.ShouldEnableInstrumentsInMeter(_configuration, "NewRelic.Test");
            var otel = _service.ShouldEnableInstrumentsInMeter(_configuration, "OpenTelemetry.Test");

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

            var result = _service.ShouldEnableInstrumentsInMeter(_configuration, "TestMeter");

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_EmptyOrNullMeterName()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns((IEnumerable<string>)null);

            var nullResult = _service.ShouldEnableInstrumentsInMeter(_configuration, null);
            var emptyResult = _service.ShouldEnableInstrumentsInMeter(_configuration, "");

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

            var result = _service.ShouldEnableInstrumentsInMeter(_configuration, "OtherMeter");

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeter_NullExcludeWithInclude()
        {
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters)
                .Returns(new List<string> { "IncludedMeter" });
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters)
                .Returns((IEnumerable<string>)null);

            var included = _service.ShouldEnableInstrumentsInMeter(_configuration, "IncludedMeter");
            var other = _service.ShouldEnableInstrumentsInMeter(_configuration, "OtherMeter");

            Assert.That(included, Is.True);
            Assert.That(other, Is.True);
        }

        [Test]
        public void IsNotBuiltInExclusion_NullOrEmpty()
        {
            var nullResult = MeterFilterService.IsNotBuiltInExclusion(null);
            var emptyResult = MeterFilterService.IsNotBuiltInExclusion("");

            Assert.That(nullResult, Is.False);
            Assert.That(emptyResult, Is.False);
        }
    }
}
