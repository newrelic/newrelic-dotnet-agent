// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Metrics
{
    public enum OtelBridgeSupportabilityMetric
    {
        BridgeEnabled,
        BridgeDisabled,
        GetMeter,
        CreateCounter,
        CreateHistogram,
        CreateUpDownCounter,
        CreateGauge,
        CreateObservableCounter,
        CreateObservableHistogram,
        CreateObservableUpDownCounter,
        CreateObservableGauge,
        InstrumentCreated,
        InstrumentBridgeFailure,
        MeasurementRecorded,
        MeasurementBridgeFailure
    }

    public interface IOtelBridgeSupportabilityMetricCounters : IOutOfBandMetricSource
    {
        void Record(OtelBridgeSupportabilityMetric metric);
    }

    public class OtelBridgeSupportabilityMetricCounters : IOtelBridgeSupportabilityMetricCounters
    {
        private readonly Dictionary<OtelBridgeSupportabilityMetric, InterlockedCounter> _counters;
        private readonly IMetricBuilder _metricBuilder;
        private PublishMetricDelegate _publishMetricDelegate;

        public OtelBridgeSupportabilityMetricCounters(IMetricBuilder metricBuilder)
        {
            _metricBuilder = metricBuilder;
            
            _counters = Enum.GetValues(typeof(OtelBridgeSupportabilityMetric))
                .Cast<OtelBridgeSupportabilityMetric>()
                .ToDictionary(x => x, x => new InterlockedCounter());
        }

        #region Public Recording Methods

        public void Record(OtelBridgeSupportabilityMetric metric)
        {
            _counters[metric].Increment();
        }



        #endregion

        #region IOutOfBandMetricSource Implementation

        public void CollectMetrics()
        {
            foreach (var kvp in _counters)
            {
                if (TryGetCount(kvp.Value, out var count))
                {
                    var metricName = GetMetricName(kvp.Key);
                    var metric = _metricBuilder.TryBuildSupportabilityCountMetric(metricName, count);
                    TrySend(metric);
                }
            }
        }

        public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate)
        {
            if (_publishMetricDelegate != null)
            {
                Log.Warn("Existing PublishMetricDelegate registration being overwritten for OpenTelemetry Bridge.");
            }

            _publishMetricDelegate = publishMetricDelegate;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Gets the metric name for a given supportability metric enum value
        /// </summary>
        private static string GetMetricName(OtelBridgeSupportabilityMetric metric)
        {
            return metric switch
            {
                OtelBridgeSupportabilityMetric.BridgeEnabled => MetricNames.SupportabilityOtelBridgeEnabled,
                OtelBridgeSupportabilityMetric.BridgeDisabled => MetricNames.SupportabilityOtelBridgeDisabled,
                OtelBridgeSupportabilityMetric.GetMeter => MetricNames.SupportabilityOtelBridgeGetMeter,
                OtelBridgeSupportabilityMetric.CreateCounter => MetricNames.SupportabilityOtelBridgeMeterCreateCounter,
                OtelBridgeSupportabilityMetric.CreateHistogram => MetricNames.SupportabilityOtelBridgeMeterCreateHistogram,
                OtelBridgeSupportabilityMetric.CreateUpDownCounter => MetricNames.SupportabilityOtelBridgeMeterCreateUpDownCounter,
                OtelBridgeSupportabilityMetric.CreateGauge => MetricNames.SupportabilityOtelBridgeMeterCreateGauge,
                OtelBridgeSupportabilityMetric.CreateObservableCounter => MetricNames.SupportabilityOtelBridgeMeterCreateObservableCounter,
                OtelBridgeSupportabilityMetric.CreateObservableHistogram => MetricNames.SupportabilityOtelBridgeMeterCreateObservableHistogram,
                OtelBridgeSupportabilityMetric.CreateObservableUpDownCounter => MetricNames.SupportabilityOtelBridgeMeterCreateObservableUpDownCounter,
                OtelBridgeSupportabilityMetric.CreateObservableGauge => MetricNames.SupportabilityOtelBridgeMeterCreateObservableGauge,
                OtelBridgeSupportabilityMetric.InstrumentCreated => MetricNames.SupportabilityOtelBridgeInstrumentCreated,
                OtelBridgeSupportabilityMetric.InstrumentBridgeFailure => MetricNames.SupportabilityOtelBridgeInstrumentBridgeFailure,
                OtelBridgeSupportabilityMetric.MeasurementRecorded => MetricNames.SupportabilityOtelBridgeMeasurementRecorded,
                OtelBridgeSupportabilityMetric.MeasurementBridgeFailure => MetricNames.SupportabilityOtelBridgeMeasurementBridgeFailure,
                _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown OtelBridgeSupportabilityMetric")
            };
        }

        /// <summary>
        /// Gets the current count from a counter and resets it to zero if there is a count.
        /// </summary>
        /// <param name="counter">The counter to check and reset</param>
        /// <param name="count">The current count if greater than zero</param>
        /// <returns>True if there was a count to report, false otherwise</returns>
        private static bool TryGetCount(InterlockedCounter counter, out int count)
        {
            count = 0;
            if (counter.Value > 0)
            {
                count = counter.Exchange(0);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sends a metric using the registered delegate.
        /// </summary>
        /// <param name="metric">The metric to send</param>
        private void TrySend(MetricWireModel metric)
        {
            if (metric == null)
                return;

            if (_publishMetricDelegate == null)
            {
                Log.Warn("No PublishMetricDelegate to flush metric '{0}' through.", metric.MetricNameModel.Name);
                return;
            }

            try
            {
                _publishMetricDelegate(metric);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TrySend() failed for metric '{0}'", metric.MetricNameModel.Name);
            }
        }

        #endregion
    }
}
