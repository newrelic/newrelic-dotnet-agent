// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Aggregators;

/// <summary>
/// A Dictionary subclass used to accumulate (merge) and hold aggregated metrics.  Given a supplied merge function,
/// knows how to merge a single new metric (Collect) or an entire ScopedMetricsStatsEngine (Harvest).
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class MetricStatsDictionary<TKey, TValue> : Dictionary<TKey, TValue>
{
    public MetricStatsDictionary()
    {
    }

    public MetricStatsDictionary(IEnumerable<KeyValuePair<TKey, TValue>> keyValuesPairs)
    {
        foreach (var kvp in keyValuesPairs)
        {
            this[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Adds <paramref name="value"/> to the dictionary under <paramref name="key"/>. If there is no existing value for <paramref name="key"/> then it will be set to <paramref name="value"/>. If there is already a value for <paramref name="key"/>, the new value will be merged with the existing value using <paramref name="mergeFunction"/>.
    /// 
    /// For safety, be sure to account for null values in <paramref name="mergeFunction"/>
    /// </summary>
    /// <param name="key">The key to merge with.</param>
    /// <param name="value">The value to merge.</param>
    /// <param name="mergeFunction">A function that will merge two values (existingValue, newValue) if an existing value is found.</param>
    public void Merge(TKey key, TValue value, Func<TValue, TValue, TValue> mergeFunction)
    {
        if (TryGetValue(key, out TValue existing))
        {
            this[key] = mergeFunction(existing, value);
        }
        else
        {
            Add(key, value);
        }
    }

    /// <summary>
    /// Helper function that merges each metric in another ScopedMetricsStatsEngine with this ScopedMetricsStatsEngine
    /// </summary>
    /// <param name="metricsToMerge"></param>
    /// <param name="mergeFunction">A function that will merge two values (existingValue, newValue) if an existing value is found.</param>
    public void Merge(IEnumerable<KeyValuePair<TKey, TValue>> metricsToMerge, Func<TValue, TValue, TValue> mergeFunction)
    {
        foreach (var metric in metricsToMerge)
        {
            Merge(metric.Key, metric.Value, mergeFunction);
        }
    }
}