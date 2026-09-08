// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Metrics;

public interface IContinuousProfilingSupportabilityMetricCounters : IOutOfBandMetricSource, IExportRetrySupportabilityMetricCounters
{
}

/// <summary>
/// Continuous profiling's own export success/retry/failure counters, dedicated so CP's send volume
/// (drains as often as every 1s) never lands on the OpenTelemetry Metrics Bridge's export/* counters
/// (a 60s cadence) -- see <see cref="IOtelBridgeSupportabilityMetricCounters"/>.
/// </summary>
public class ContinuousProfilingSupportabilityMetricCounters : IContinuousProfilingSupportabilityMetricCounters
{
    private readonly InterlockedCounter _successCounter = new InterlockedCounter();
    private readonly InterlockedCounter _retryCounter = new InterlockedCounter();
    private readonly InterlockedCounter _failureCounter = new InterlockedCounter();
    private readonly IMetricBuilder _metricBuilder;
    private PublishMetricDelegate _publishMetricDelegate;
    private bool _loggedMissingDelegateError;

    public ContinuousProfilingSupportabilityMetricCounters(IMetricBuilder metricBuilder)
    {
        _metricBuilder = metricBuilder;
    }

    public void RecordExportSuccess() => _successCounter.Increment();
    public void RecordExportRetry() => _retryCounter.Increment();
    public void RecordExportFailure() => _failureCounter.Increment();

    public void CollectMetrics()
    {
        TryReportAndReset(_successCounter, MetricNames.SupportabilityContinuousProfilingExportSuccess);
        TryReportAndReset(_failureCounter, MetricNames.SupportabilityContinuousProfilingExportFailure);
        TryReportAndReset(_retryCounter, MetricNames.SupportabilityContinuousProfilingExportRetry);
    }

    public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate)
    {
        if (_publishMetricDelegate != null)
        {
            Log.Warn("Existing PublishMetricDelegate registration being overwritten for Continuous Profiling.");
        }

        _publishMetricDelegate = publishMetricDelegate;
    }

    private void TryReportAndReset(InterlockedCounter counter, string metricName)
    {
        if (counter.Value <= 0)
            return;

        var count = counter.Exchange(0);
        var metric = _metricBuilder.TryBuildSupportabilityCountMetric(metricName, count);
        TrySend(metric);
    }

    private void TrySend(MetricWireModel metric)
    {
        if (metric == null)
            return;

        if (_publishMetricDelegate == null)
        {
            if (!_loggedMissingDelegateError)
            {
                Log.Error("No PublishMetricDelegate registered. Continuous profiling supportability metrics will not be reported. This indicates an agent initialization error.");
                _loggedMissingDelegateError = true;
            }
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
}
