// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

/// <summary>
/// Test assertion helpers for metric dictionaries produced by
/// <see cref="KafkaStatisticsHelper"/>. Prefer these over the bare
/// <c>Assert.That(metrics, Contains.Key("…"))</c> form — the failure messages
/// list nearby keys so you can see the likely typo or rename at a glance.
/// </summary>
internal static class MetricAssertions
{
    /// <summary>
    /// Asserts that <paramref name="expectedKey"/> is present in <paramref name="metrics"/>.
    /// On failure, lists up to 10 keys that share the longest possible prefix with the
    /// expected key, so the message points at likely typos or renamed paths instead of
    /// just "key missing".
    /// </summary>
    public static void ExpectKey(Dictionary<string, KafkaMetricValue> metrics, string expectedKey)
    {
        if (metrics.ContainsKey(expectedKey))
            return;

        // Walk the expected key back character-by-character until we find prefixes in the
        // dict. The first non-empty match is the "nearby misses" set for the failure message.
        var prefix = expectedKey;
        List<string> similar = null;
        while (prefix.Length > 0)
        {
            similar = metrics.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(k => k, StringComparer.Ordinal)
                .Take(10)
                .ToList();
            if (similar.Count > 0)
                break;
            prefix = prefix.Substring(0, prefix.Length - 1);
        }

        var msg = similar != null && similar.Count > 0
            ? $"Expected metric '{expectedKey}' not present.{Environment.NewLine}Similar keys matching prefix '{prefix}':{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", similar)
            : $"Expected metric '{expectedKey}' not present, and no keys share any prefix. Total keys: {metrics.Count}";
        Assert.Fail(msg);
    }

    /// <summary>
    /// Asserts that <paramref name="expectedAbsentKey"/> is NOT present in
    /// <paramref name="metrics"/>. Used for zero-filtering tests and rename-regression guards.
    /// </summary>
    public static void ExpectNoKey(Dictionary<string, KafkaMetricValue> metrics, string expectedAbsentKey)
    {
        if (!metrics.ContainsKey(expectedAbsentKey))
            return;

        Assert.Fail($"Metric '{expectedAbsentKey}' was present but should have been filtered. Value: {metrics[expectedAbsentKey].Value}, Type: {metrics[expectedAbsentKey].MetricType}");
    }
}
