// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NewRelic.Testing.Assertions;

/// <summary>
/// Test assertion helpers for metric dictionaries keyed by metric name. Prefer these
/// over bare <c>Assert.That(metrics, Contains.Key("…"))</c> — on failure, ExpectKey
/// lists the keys that share the longest common prefix with the expected one, so the
/// failure message points at the likely typo or rename instead of just "key missing".
/// </summary>
public static class MetricAssertions
{
    /// <summary>
    /// Asserts that <paramref name="expectedKey"/> is present in <paramref name="metrics"/>.
    /// On failure, lists up to 10 keys that share the longest possible prefix with the
    /// expected key.
    /// </summary>
    public static void ExpectKey<TValue>(IDictionary<string, TValue> metrics, string expectedKey)
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
    /// <paramref name="metrics"/>. Useful for filter-behavior checks and rename-regression guards.
    /// </summary>
    public static void ExpectNoKey<TValue>(IDictionary<string, TValue> metrics, string expectedAbsentKey)
    {
        if (!metrics.ContainsKey(expectedAbsentKey))
            return;

        Assert.Fail($"Metric '{expectedAbsentKey}' was present but should have been absent. Value: {metrics[expectedAbsentKey]}");
    }
}
