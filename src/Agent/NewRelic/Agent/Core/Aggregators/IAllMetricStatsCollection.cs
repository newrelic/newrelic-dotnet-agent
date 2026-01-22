// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Aggregators;

/// <summary>
/// Used to pass metric data to the Metric Aggregator.
/// </summary>
public interface IAllMetricStatsCollection
{
    void AddMetricsToCollection(MetricStatsCollection collection);
}
