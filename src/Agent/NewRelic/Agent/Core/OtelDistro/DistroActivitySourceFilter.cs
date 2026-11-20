// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// The distro's ActivitySource exclude list: the hybrid default list
/// (<see cref="IConfiguration.OpenTelemetryTracingDefaultExcludedActivitySources"/>, the one agent mode uses) plus the
/// generated additions, ActivitySource names of OTel instrumentation that duplicates an NR-instrumented library in
/// distro mode only. The additions sit at the same priority as the default list: a customer include overrides them,
/// and a customer exclude overrides an include. Immutable; agent mode never uses this type.
/// </summary>
public class DistroActivitySourceFilter
{
    private readonly string[] _additions;

    public DistroActivitySourceFilter(IEnumerable<string> additions)
    {
        _additions = additions?.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray() ?? new string[0];
    }

    public IReadOnlyList<string> Additions => _additions;

    public bool ShouldListenTo(string activitySourceName, IConfiguration configuration)
    {
        var isAddition = _additions.Contains(activitySourceName, StringComparer.CurrentCultureIgnoreCase);
        if (isAddition && !configuration.OpenTelemetryTracingIncludedActivitySources.Contains(activitySourceName, StringComparer.CurrentCultureIgnoreCase))
        {
            Log.Finest($"ShouldListenToActivitySource: Not listening to {activitySourceName} (distro exclude list).");
            return false;
        }

        // Not an addition, or the customer included it: the hybrid rules decide (default list, customer include/exclude).
        return ActivitySourceExtensions.ShouldListenToActivitySource(activitySourceName, configuration);
    }
}
