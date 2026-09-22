// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Metrics;

/// <summary>
/// The subset of export-outcome recording that <see cref="NewRelic.Agent.Core.DataTransport.CustomRetryHandler"/>
/// needs from a caller's supportability counters. Narrower than <see cref="IOtelBridgeSupportabilityMetricCounters"/>
/// so a second, independent implementation (e.g. continuous profiling's own counters) can be plugged into the
/// same retry handler without sharing -- or depending on -- the OpenTelemetry Metrics Bridge's counters.
/// </summary>
public interface IExportRetrySupportabilityMetricCounters
{
    void RecordExportSuccess();
    void RecordExportRetry();
    void RecordExportFailure();
}
