// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// The one queue for distro-mode supportability count metrics: the OTel configuration checks
/// (<c>Supportability/Dotnet/OtelDistro/Config/*</c>), the "OTel wins" precedence reports and the method-resolution
/// fallback counts of <see cref="DistroMethodResolutionDiagnostics"/>. Names recorded while no sink is set are counted
/// (per name, so the queue stays bounded however often a name is recorded) and flushed, in first-recorded order, when a
/// sink is set. <see cref="DistroAgentManager"/> sets the sink only once the agent has connected: Core's metric aggregator
/// discards everything recorded before connect (it replaces its stats queue on the configuration update connect
/// publishes), and most of these names are recorded during OTel start-up, before connect. Never throws.
/// </summary>
public static class DistroConfigMetrics
{
    private static readonly object Gate = new object();
    private static readonly List<string> PendingOrder = new List<string>();
    private static readonly Dictionary<string, int> PendingCounts = new Dictionary<string, int>();

    private static Action<string> _sink;

    /// <summary>
    /// Records <paramref name="count"/> counts of <paramref name="metricName"/> under a single <see cref="Gate"/>
    /// acquisition (or queues the batch when no sink is set). Lets a caller on a hot path accumulate lock-free and record
    /// the batch once, instead of taking the gate per occurrence. A non-positive count is ignored.
    /// </summary>
    public static void Record(string metricName, int count)
    {
        if (count <= 0)
        {
            return;
        }

        try
        {
            Action<string> sink;
            lock (Gate)
            {
                sink = _sink;
                if (sink == null)
                {
                    if (PendingCounts.TryGetValue(metricName, out var pending))
                    {
                        PendingCounts[metricName] = pending + count;
                    }
                    else
                    {
                        PendingCounts[metricName] = count;
                        PendingOrder.Add(metricName);
                    }

                    return;
                }
            }

            for (var i = 0; i < count; i++)
            {
                sink(metricName);
            }
        }
        catch (Exception)
        {
            // Diagnostics must never reach OTel start-up or an instrumented method.
        }
    }

    /// <summary>Records one count of <paramref name="metricName"/>, or queues it when no sink is set.</summary>
    public static void Record(string metricName) => Record(metricName, 1);

    /// <summary>
    /// Sets where metric names are sent and flushes names recorded before a sink existed. Null detaches the sink (names
    /// are queued again until the next one is set).
    /// </summary>
    public static void SetMetricSink(Action<string> sink)
    {
        var pending = new List<KeyValuePair<string, int>>();
        lock (Gate)
        {
            _sink = sink;
            if (sink == null)
            {
                return;
            }

            foreach (var metricName in PendingOrder)
            {
                pending.Add(new KeyValuePair<string, int>(metricName, PendingCounts[metricName]));
            }

            PendingOrder.Clear();
            PendingCounts.Clear();
        }

        try
        {
            foreach (var entry in pending)
            {
                for (var i = 0; i < entry.Value; i++)
                {
                    sink(entry.Key);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Test seam: drops queued names and detaches the sink.</summary>
    public static void ResetForTesting()
    {
        lock (Gate)
        {
            _sink = null;
            PendingOrder.Clear();
            PendingCounts.Clear();
        }
    }
}
