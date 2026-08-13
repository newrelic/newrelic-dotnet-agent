// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.ContinuousProfiling;

namespace NewRelic.Agent.Core.Commands;

/// <summary>
/// The detailed response proposed in the DACI's "Additional Notes" section (not yet decided): the profile
/// types currently profiling, the effective intervals, and any per-type exceptions ("not supported" for
/// heap/allocations, since that isn't implemented yet). Only adds the "exceptions" key when there is at
/// least one exception, matching the proposed spec's "the agent response payload includes exceptions if
/// present" wording.
/// </summary>
public class DetailedContinuousProfilerResponseFormatter : IContinuousProfilerCommandResponseFormatter
{
    public IDictionary<string, object> Format(ContinuousProfilingCommandResult result)
    {
        var payload = new Dictionary<string, object>
        {
            { "include", result.ActiveTypes },
            { "sample_interval", result.SampleIntervalMs },
            { "cpu_report_interval", result.CpuReportIntervalMs }
        };

        if (result.Exceptions.Count > 0)
            payload["exceptions"] = result.Exceptions;

        return payload;
    }
}
