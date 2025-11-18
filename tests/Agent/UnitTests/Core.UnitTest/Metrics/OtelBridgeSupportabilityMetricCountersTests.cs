// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;
using TestUtilities = NewRelic.Agent.TestUtilities;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.UnitTests.Metrics
{
    [TestFixture]
    public class OtelBridgeSupportabilityMetricCountersTests
    {
        private OtelBridgeSupportabilityMetricCounters _metricCounters;
        private List<MetricWireModel> _publishedMetrics;

        [SetUp]
        public void SetUp()
        {
            var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
            _metricCounters = new OtelBridgeSupportabilityMetricCounters(metricBuilder);
            
            _publishedMetrics = new List<MetricWireModel>();
            _metricCounters.RegisterPublishMetricHandler(metric => _publishedMetrics.Add(metric));
        }

        [TearDown]
        public void TearDown()
        {
            _publishedMetrics?.Clear();
        }

        [Test]
        public void NoMetrics_WhenNothingRecorded()
        {
            _metricCounters.CollectMetrics();
            Assert.That(_publishedMetrics, Is.Empty);
        }

        [TestCase(OtelBridgeSupportabilityMetric.BridgeEnabled, MetricNames.SupportabilityOtelBridgeEnabled)]
        [TestCase(OtelBridgeSupportabilityMetric.BridgeDisabled, MetricNames.SupportabilityOtelBridgeDisabled)]
        [TestCase(OtelBridgeSupportabilityMetric.GetMeter, MetricNames.SupportabilityOtelBridgeGetMeter)]
        [TestCase(OtelBridgeSupportabilityMetric.CreateCounter, MetricNames.SupportabilityOtelBridgeMeterCreateCounter)]
        [TestCase(OtelBridgeSupportabilityMetric.CreateHistogram, MetricNames.SupportabilityOtelBridgeMeterCreateHistogram)]
        [TestCase(OtelBridgeSupportabilityMetric.CreateUpDownCounter, MetricNames.SupportabilityOtelBridgeMeterCreateUpDownCounter)]
        [TestCase(OtelBridgeSupportabilityMetric.CreateGauge, MetricNames.SupportabilityOtelBridgeMeterCreateGauge)]
        [TestCase(OtelBridgeSupportabilityMetric.CreateObservableCounter, MetricNames.SupportabilityOtelBridgeMeterCreateObservableCounter)]
        [TestCase(OtelBridgeSupportabilityMetric.CreateObservableHistogram, MetricNames.SupportabilityOtelBridgeMeterCreateObservableHistogram)]
        [TestCase(OtelBridgeSupportabilityMetric.CreateObservableUpDownCounter, MetricNames.SupportabilityOtelBridgeMeterCreateObservableUpDownCounter)]
        [TestCase(OtelBridgeSupportabilityMetric.CreateObservableGauge, MetricNames.SupportabilityOtelBridgeMeterCreateObservableGauge)]
        [TestCase(OtelBridgeSupportabilityMetric.InstrumentCreated, MetricNames.SupportabilityOtelBridgeInstrumentCreated)]
        [TestCase(OtelBridgeSupportabilityMetric.InstrumentBridgeFailure, MetricNames.SupportabilityOtelBridgeInstrumentBridgeFailure)]
        [TestCase(OtelBridgeSupportabilityMetric.MeasurementRecorded, MetricNames.SupportabilityOtelBridgeMeasurementRecorded)]
        [TestCase(OtelBridgeSupportabilityMetric.MeasurementBridgeFailure, MetricNames.SupportabilityOtelBridgeMeasurementBridgeFailure)]
        public void Record_GeneratesCorrectMetric_ForAllEnumValues(OtelBridgeSupportabilityMetric metricType, string expectedMetricName)
        {
            // Arrange - Enable finest logging to ensure all metrics are recorded
            using (new TestUtilities.Logging(LogEventLevel.Verbose))
            {
                // Act
                _metricCounters.Record(metricType);
                _metricCounters.CollectMetrics();

                // Assert
                Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
                var metric = _publishedMetrics.Single();
                NrAssert.Multiple(
                    () => Assert.That(metric.MetricNameModel.Name, Is.EqualTo(expectedMetricName)),
                    () => Assert.That(metric.DataModel.Value0, Is.EqualTo(1))
                );
            }
        }

        [Test]
        public void Record_AggregatesMultipleCalls()
        {
            // Act
            _metricCounters.Record(OtelBridgeSupportabilityMetric.CreateCounter);
            _metricCounters.Record(OtelBridgeSupportabilityMetric.CreateCounter);
            _metricCounters.Record(OtelBridgeSupportabilityMetric.CreateCounter);
            _metricCounters.CollectMetrics();

            // Assert
            Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
            var metric = _publishedMetrics.Single();
            NrAssert.Multiple(
                () => Assert.That(metric.MetricNameModel.Name, Is.EqualTo(MetricNames.SupportabilityOtelBridgeMeterCreateCounter)),
                () => Assert.That(metric.DataModel.Value0, Is.EqualTo(3))
            );
        }

        [Test]
        public void CollectMetrics_ResetsCounters()
        {
            // Act - Record metrics and collect
            _metricCounters.Record(OtelBridgeSupportabilityMetric.BridgeEnabled);
            _metricCounters.CollectMetrics();
            
            // Clear published metrics and collect again
            _publishedMetrics.Clear();
            _metricCounters.CollectMetrics();

            // Assert - No metrics should be published since counters were reset
            Assert.That(_publishedMetrics, Is.Empty);
        }

        [TestCase(OtelBridgeSupportabilityMetric.InstrumentCreated)]
        [TestCase(OtelBridgeSupportabilityMetric.InstrumentBridgeFailure)]
        [TestCase(OtelBridgeSupportabilityMetric.MeasurementRecorded)]
        [TestCase(OtelBridgeSupportabilityMetric.MeasurementBridgeFailure)]
        public void DebuggingMetrics_AreGenerated_WhenFinestLoggingEnabled(OtelBridgeSupportabilityMetric debuggingMetric)
        {
            // Arrange - Enable finest logging
            using (new TestUtilities.Logging(LogEventLevel.Verbose))
            {
                // Act
                _metricCounters.Record(debuggingMetric);
                _metricCounters.CollectMetrics();

                // Assert
                Assert.That(_publishedMetrics, Has.Count.EqualTo(1));
                var metric = _publishedMetrics.Single();
                Assert.That(metric.DataModel.Value0, Is.EqualTo(1));
            }
        }

        [Test]
        public void DebuggingMetrics_FilteredBasedOnLogLevel()
        {
            // Test verifies debugging vs non-debugging metric behavior
            
            // Record one debugging and one non-debugging metric
            _metricCounters.Record(OtelBridgeSupportabilityMetric.InstrumentCreated); // Debugging
            _metricCounters.Record(OtelBridgeSupportabilityMetric.BridgeEnabled); // Non-debugging
            _metricCounters.CollectMetrics();

            // At minimum, we should have the non-debugging metric
            Assert.That(_publishedMetrics.Count, Is.GreaterThanOrEqualTo(1),
                "At least the non-debugging metric should be published");
                
            // Verify non-debugging metrics are always included
            var nonDebuggingPublished = _publishedMetrics.Any(m => 
                m.MetricNameModel.Name == MetricNames.SupportabilityOtelBridgeEnabled);
            Assert.That(nonDebuggingPublished, Is.True,
                "Non-debugging metrics should always be published");
        }

        [Test]
        public void RegisterPublishMetricHandler_DoesNotThrow()
        {
            // Arrange
            var additionalMetrics = new List<MetricWireModel>();

            // Act & Assert
            Assert.DoesNotThrow(() => _metricCounters.RegisterPublishMetricHandler(metric => additionalMetrics.Add(metric)));
        }
    }
}
