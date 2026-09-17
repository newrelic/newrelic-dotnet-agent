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
    /// <summary>
    /// A drain's compressed payload exceeded <c>IConfiguration.CollectorMaxPayloadSizeInBytes</c> and was
    /// dropped client-side without attempting a send -- see <c>OtlpProfilesHttpDispatcher.Post</c>. Kept
    /// separate from <see cref="IExportRetrySupportabilityMetricCounters.RecordExportFailure"/> because the
    /// request never reached the wire; a 413 that does reach the wire still counts as a failure via retry.
    /// </summary>
    void RecordPayloadDropped();

    /// <summary>
    /// A 2xx response whose OTLP <c>partial_success</c> reported every profile in the batch rejected --
    /// see <c>ProfilesTransport.Send</c>. Kept separate from
    /// <see cref="IExportRetrySupportabilityMetricCounters.RecordExportSuccess"/>, which this outcome
    /// still also records (the HTTP delivery itself succeeded) -- this counter exists so a 100%-rejected
    /// partial_success is distinguishable from an ordinary, genuinely-delivered success.
    /// </summary>
    void RecordFullRejection();
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
    private readonly InterlockedCounter _payloadDroppedCounter = new InterlockedCounter();
    private readonly InterlockedCounter _fullRejectionCounter = new InterlockedCounter();
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
    public void RecordPayloadDropped() => _payloadDroppedCounter.Increment();
    public void RecordFullRejection() => _fullRejectionCounter.Increment();

    public void CollectMetrics()
    {
        TryReportAndReset(_successCounter, MetricNames.SupportabilityContinuousProfilingExportSuccess);
        TryReportAndReset(_failureCounter, MetricNames.SupportabilityContinuousProfilingExportFailure);
        TryReportAndReset(_retryCounter, MetricNames.SupportabilityContinuousProfilingExportRetry);
        TryReportAndReset(_payloadDroppedCounter, MetricNames.SupportabilityContinuousProfilingExportPayloadDropped);
        TryReportAndReset(_fullRejectionCounter, MetricNames.SupportabilityContinuousProfilingExportFullRejection);
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
