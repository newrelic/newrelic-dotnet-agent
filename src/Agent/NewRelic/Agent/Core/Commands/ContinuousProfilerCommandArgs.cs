// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Core.Commands;

/// <summary>
/// Parses the arguments of a start_continuous_profiler/stop_continuous_profiler agent command. Wire
/// argument names are snake_case ("include", "sample_interval", "cpu_report_interval"), matching every
/// other agent command in this codebase (profile_id, sample_period, report_data) -- the DACI's GraphQL
/// block (sampleInterval/cpuReportInterval) belongs to the separate sibling backend service's public API,
/// not the agent_commands wire shape sent to the agent.
/// </summary>
public class ContinuousProfilerCommandArgs
{
    public IReadOnlyList<string> Include { get; }
    public int? SampleIntervalMs { get; }
    public int? CpuReportIntervalMs { get; }

    public ContinuousProfilerCommandArgs(IDictionary<string, object> arguments)
    {
        Include = ParseInclude(arguments);
        SampleIntervalMs = ParsePositiveInt(arguments, "sample_interval");
        CpuReportIntervalMs = ParsePositiveInt(arguments, "cpu_report_interval");
    }

    private static IReadOnlyList<string> ParseInclude(IDictionary<string, object> arguments)
    {
        if (!arguments.TryGetValue("include", out var raw) || raw == null)
            return Array.Empty<string>();

        if (raw is JArray jArray)
            return jArray.Select(token => token.ToString()).ToList();

        // Defensive: a single bare string ("all") instead of a one-element array.
        return new[] { raw.ToString() };
    }

    private static int? ParsePositiveInt(IDictionary<string, object> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var raw) || raw == null)
            return null;

        if (!int.TryParse(raw.ToString(), out var parsed) || parsed <= 0)
            return null;

        return parsed;
    }
}
