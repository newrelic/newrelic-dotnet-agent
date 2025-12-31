// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge
{
    [TestFixture]
    public class MeterBridgingServiceTests
    {
        private IMeterListenerWrapper _mockListener;
        private IConfigurationService _mockConfigService;
        private IConfiguration _mockConfig;
        private IOtelBridgeSupportabilityMetricCounters _mockMetrics;
        private MeterBridgingService _service;

        [SetUp]
        public void SetUp()
        {
            _mockListener = Mock.Create<IMeterListenerWrapper>();
            _mockConfigService = Mock.Create<IConfigurationService>();
            _mockConfig = Mock.Create<IConfiguration>();
            _mockMetrics = Mock.Create<IOtelBridgeSupportabilityMetricCounters>();

            Mock.Arrange(() => _mockConfigService.Configuration).Returns(_mockConfig);
            Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsIncludeFilters).Returns((IEnumerable<string>)null);
            Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsExcludeFilters).Returns((IEnumerable<string>)null);

            _service = new MeterBridgingService(_mockListener, _mockConfigService, _mockMetrics);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            (_mockListener as IDisposable)?.Dispose();
        }

        [Test]
        public void StartListening_CallsListenerStart()
        {
            // Act
            _service.StartListening(null);

            // Assert
            Mock.Assert(() => _mockListener.Start(), Occurs.Once());
        }

        [Test]
        public void StopListening_DisposesListener()
        {
            // Act
            _service.StopListening();

            // Assert
            Mock.Assert(() => _mockListener.Dispose(), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_WithNullMeter_DoesNotThrow()
        {
            // Arrange
            var mockInstrument = new { };

            // Act & Assert
            Assert.DoesNotThrow(() => _service.OnInstrumentPublished(mockInstrument, _mockListener));
        }

        [Test]
        public void OnInstrumentPublished_WithFilteredMeter_DoesNotEnableInstrument()
        {
            // Arrange
            Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsExcludeFilters).Returns(new[] { "TestMeter" });

            // Act
            _service.OnInstrumentPublished(null, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Never());
        }

        [Test]
        public void Dispose_DisposesListener()
        {
            // Act
            _service.Dispose();

            // Assert
            Mock.Assert(() => _mockListener.Dispose(), Occurs.Once());
        }
    }
}
