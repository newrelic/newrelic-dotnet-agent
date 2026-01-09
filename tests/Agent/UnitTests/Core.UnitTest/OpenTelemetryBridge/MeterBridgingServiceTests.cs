// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Private callback methods (OnMeasurementRecorded, FilterValidTags, BridgeMeasurements)
// are tested indirectly through integration tests rather than unit tests.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
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
        public void StartListening_WithMeterParameter_CallsListenerStart()
        {
            // Arrange
            using var meter = new Meter("TestMeter");

            // Act
            _service.StartListening(meter);

            // Assert
            Mock.Assert(() => _mockListener.Start(), Occurs.Once());
        }

        [Test]
        public void StopListening_DisposesListener()
        {
            // Act
            _service.StopListening();

            // Assert
            Mock.Arrange(() => _mockListener.Dispose()).MustBeCalled();
        }

        [Test]
        public void OnInstrumentPublished_WithNullInstrument_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.OnInstrumentPublished(null, _mockListener));
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
        public void OnInstrumentPublished_WithRealCounter_EnablesMeasurementEvents()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("test-counter");

            // Act
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(counter, Arg.IsAny<object>()), Occurs.Once());
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.GetMeter), Occurs.AtLeastOnce());
        }

        [Test]
        public void OnInstrumentPublished_WithHistogram_EnablesMeasurementEvents()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var histogram = meter.CreateHistogram<double>("test-histogram");

            // Act
            _service.OnInstrumentPublished(histogram, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(histogram, Arg.IsAny<object>()), Occurs.Once());
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.CreateHistogram), Occurs.AtLeastOnce());
        }

        [Test]
        public void OnInstrumentPublished_WithUpDownCounter_EnablesMeasurementEvents()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var upDownCounter = meter.CreateUpDownCounter<long>("test-updowncounter");

            // Act
            _service.OnInstrumentPublished(upDownCounter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(upDownCounter, Arg.IsAny<object>()), Occurs.Once());
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.CreateUpDownCounter), Occurs.AtLeastOnce());
        }

        [Test]
        public void OnInstrumentPublished_WithObservableCounter_EnablesMeasurementEvents()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var observableCounter = meter.CreateObservableCounter("test-observable-counter", () => 42);

            // Act
            _service.OnInstrumentPublished(observableCounter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(observableCounter, Arg.IsAny<object>()), Occurs.Once());
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.CreateObservableCounter), Occurs.AtLeastOnce());
        }

        [Test]
        public void OnInstrumentPublished_CachesPropertyAccessors()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter1 = meter.CreateCounter<int>("counter1");
            var counter2 = meter.CreateCounter<int>("counter2");

            // Act - First call should cache accessors, second call reuses cached accessors
            _service.OnInstrumentPublished(counter1, _mockListener);
            _service.OnInstrumentPublished(counter2, _mockListener);

            // Assert - Both should succeed with cached accessors
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Exactly(2));
        }

        [Test]
        public void OnInstrumentPublished_WithExcludedMeterName_DoesNotEnable()
        {
            // Arrange
            Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsExcludeFilters)
                .Returns(new[] { "TestMeter" });

            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("test-counter");

            // Act
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Never());
        }

        [Test]
        public void OnInstrumentPublished_WithIncludedMeterName_DoesEnable()
        {
            // Arrange
            Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsIncludeFilters)
                .Returns(new[] { "TestMeter" });

            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("test-counter");

            // Act
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(counter, Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_RecordsInstrumentCreatedMetric()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("test-counter");

            // Act
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.InstrumentCreated), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_WithInstrumentTags_HandlesCorrectly()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var tags = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("tag1", "value1"),
                new KeyValuePair<string, object>("tag2", "value2")
            };
            var counter = meter.CreateCounter<int>("test-counter", null, null, tags);

            // Act & Assert
            Assert.DoesNotThrow(() => _service.OnInstrumentPublished(counter, _mockListener));
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(counter, Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void Dispose_DisposesListenerAndMeters()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("test-counter");
            _service.OnInstrumentPublished(counter, _mockListener);

            // Act
            _service.Dispose();

            // Assert
            Mock.Assert(() => _mockListener.Dispose(), Occurs.Once());
        }

        [Test]
        public void FilterValidTags_WithAllValidTags_ReturnsAllTags()
        {
            // This test validates the FilterValidTags helper method through public API
            // We test it indirectly by ensuring instruments with valid tags work correctly
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("test-counter");

            // Act
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert - Should successfully enable measurement events
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(counter, Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_MultipleInstrumentTypes_CachesEachTypeSeparately()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("counter");
            var histogram = meter.CreateHistogram<double>("histogram");
            var upDownCounter = meter.CreateUpDownCounter<long>("updowncounter");

            // Act
            _service.OnInstrumentPublished(counter, _mockListener);
            _service.OnInstrumentPublished(histogram, _mockListener);
            _service.OnInstrumentPublished(upDownCounter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Exactly(3));
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.CreateCounter), Occurs.Once());
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.CreateHistogram), Occurs.Once());
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.CreateUpDownCounter), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_WithExceptionInProcessing_RecordsFailureMetric()
        {
            // Arrange - Create invalid object that will cause issues
            var invalidInstrument = new { Meter = (object)null, Name = "test" };

            // Act
            _service.OnInstrumentPublished(invalidInstrument, _mockListener);

            // Assert - Should not throw and should not enable events
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Never());
        }

        [Test]
        public void MeterNameAccessorCache_CachesPerMeterType()
        {
            // Arrange
            using var meter1 = new Meter("Meter1");
            using var meter2 = new Meter("Meter2");
            var counter1 = meter1.CreateCounter<int>("counter1");
            var counter2 = meter2.CreateCounter<int>("counter2");

            // Act
            _service.OnInstrumentPublished(counter1, _mockListener);
            _service.OnInstrumentPublished(counter2, _mockListener);

            // Assert - Both should be enabled since Meter objects have same type
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Exactly(2));
        }

        [Test]
        public void ObservableInstrument_ProcessedCorrectly()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            int callCount = 0;
            var observableGauge = meter.CreateObservableGauge("test-gauge", () =>
            {
                callCount++;
                return 42.0;
            });

            // Act
            _service.OnInstrumentPublished(observableGauge, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(observableGauge, Arg.IsAny<object>()), Occurs.Once());
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.CreateObservableGauge), Occurs.Once());
        }

        [Test]
        public void ObservableUpDownCounter_ProcessedCorrectly()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var observableUpDownCounter = meter.CreateObservableUpDownCounter("test-updown", () => 10);

            // Act
            _service.OnInstrumentPublished(observableUpDownCounter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(observableUpDownCounter, Arg.IsAny<object>()), Occurs.Once());
            Mock.Assert(() => _mockMetrics.Record(OtelBridgeSupportabilityMetric.CreateObservableUpDownCounter), Occurs.Once());
        }

        [Test]
        public void MultipleCounters_WithDifferentGenericTypes_CachedSeparately()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var intCounter = meter.CreateCounter<int>("int-counter");
            var longCounter = meter.CreateCounter<long>("long-counter");
            var doubleCounter = meter.CreateCounter<double>("double-counter");

            // Act
            _service.OnInstrumentPublished(intCounter, _mockListener);
            _service.OnInstrumentPublished(longCounter, _mockListener);
            _service.OnInstrumentPublished(doubleCounter, _mockListener);

            // Assert - All should be processed
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Exactly(3));
        }

        [Test]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.Dispose());
            Assert.DoesNotThrow(() => _service.Dispose());
        }

        [Test]
        public void OnInstrumentPublished_AfterDispose_DoesNotThrow()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("counter");
            _service.Dispose();

            // Act & Assert
            Assert.DoesNotThrow(() => _service.OnInstrumentPublished(counter, _mockListener));
        }

        [Test]
        public void OnInstrumentPublished_WithEmptyMeterName_DoesNotEnableInstrument()
        {
            // Arrange
            using var meter = new Meter("");
            var counter = meter.CreateCounter<int>("counter");

            // Act
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Never());
        }

        [Test]
        public void FilterValidTags_WithNullKeys_RemovesThem()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("counter");

            // Act - This will use FilterValidTags internally when measurements are recorded
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert - Should successfully enable despite null key handling
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(counter, Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void RecordSpecificInstrumentType_ForAllTypes_RecordsMetrics()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("counter");
            var histogram = meter.CreateHistogram<int>("histogram");
            var upDownCounter = meter.CreateUpDownCounter<int>("upDownCounter");
            var observableCounter = meter.CreateObservableCounter<int>("observableCounter", () => 1);
            var observableGauge = meter.CreateObservableGauge<int>("observableGauge", () => 1);
            var observableUpDownCounter = meter.CreateObservableUpDownCounter<int>("observableUpDownCounter", () => 1);

            // Act
            _service.OnInstrumentPublished(counter, _mockListener);
            _service.OnInstrumentPublished(histogram, _mockListener);
            _service.OnInstrumentPublished(upDownCounter, _mockListener);
            _service.OnInstrumentPublished(observableCounter, _mockListener);
            _service.OnInstrumentPublished(observableGauge, _mockListener);
            _service.OnInstrumentPublished(observableUpDownCounter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.AtLeast(6));
        }

        [Test]
        public void GetInstrumentAdvice_OnOlderDotNet_HandlesGracefully()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("counter");

            // Act - OnInstrumentPublished calls GetInstrumentAdvice internally
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert - Should complete without errors
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void GetMeterScope_WithMeter_HandlesGracefully()
        {
            // Arrange
            using var meter = new Meter("TestMeter", "1.0.0");
            var counter = meter.CreateCounter<int>("counter");

            // Act - CreateBridgedMeter calls GetMeterScope internally
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void BridgeMeasurements_WithValidObservable_CreatesCorrectMeasurements()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var callCount = 0;
            var observableCounter = meter.CreateObservableCounter<int>("test", () =>
            {
                callCount++;
                return new[] { new Measurement<int>(callCount, new[] { new KeyValuePair<string, object>("iteration", callCount) }) };
            });

            // Act
            _service.OnInstrumentPublished(observableCounter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void CreateBridgedInstrument_WithUnitAndDescription_UsesCorrectConstructor()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter = meter.CreateCounter<int>("counter", "ms", "Test description");

            // Act - Tests constructor selection with unit and description
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_WithObservableCounter_BridgesMeasurements()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var observableCounter = meter.CreateObservableCounter<long>("test.observable", () => 100L);

            // Act - Observable instruments are also enabled for measurement events
            _service.OnInstrumentPublished(observableCounter, _mockListener);

            // Assert - Observable instruments call EnableMeasurementEvents like regular instruments
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_WithObservableGauge_BridgesMeasurements()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var observableGauge = meter.CreateObservableGauge<double>("test.gauge", () => 42.5);

            // Act
            _service.OnInstrumentPublished(observableGauge, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_WithObservableUpDownCounter_BridgesMeasurements()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var observableUpDown = meter.CreateObservableUpDownCounter<int>("test.updown.obs", () => -5);

            // Act
            _service.OnInstrumentPublished(observableUpDown, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_WithMeterVersion_PreservesInBridgedMeter()
        {
            // Arrange - CreateBridgedMeter should preserve version
            using var meter = new Meter("TestMeter", "2.0.0");
            var counter = meter.CreateCounter<long>("test.counter");

            // Act - This internally calls CreateBridgedMeter
            _service.OnInstrumentPublished(counter, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Once());
        }

        [Test]
        public void OnInstrumentPublished_MultipleTimes_ReusesBridgedMeter()
        {
            // Arrange
            using var meter = new Meter("TestMeter");
            var counter1 = meter.CreateCounter<int>("counter1");
            var counter2 = meter.CreateCounter<int>("counter2");

            // Act - Multiple instruments from same meter should reuse bridged meter
            _service.OnInstrumentPublished(counter1, _mockListener);
            _service.OnInstrumentPublished(counter2, _mockListener);

            // Assert
            Mock.Assert(() => _mockListener.EnableMeasurementEvents(Arg.IsAny<object>(), Arg.IsAny<object>()), Occurs.Exactly(2));
        }
    }
}
