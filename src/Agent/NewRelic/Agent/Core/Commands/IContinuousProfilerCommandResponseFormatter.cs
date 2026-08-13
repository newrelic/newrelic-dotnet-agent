// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.ContinuousProfiling;

namespace NewRelic.Agent.Core.Commands;

/// <summary>
/// Builds the start_continuous_profiler/stop_continuous_profiler agent command response payload from a
/// response-shape-agnostic <see cref="ContinuousProfilingCommandResult"/>. Exists as a seam because the
/// DACI is undecided between two shapes: the plain agent-command ack the main spec body says is
/// sufficient (<see cref="AckOnlyContinuousProfilerResponseFormatter"/>), and the detailed
/// include/sample_interval/cpu_report_interval/exceptions payload proposed in the DACI's (still-open)
/// "Additional Notes" section (<see cref="DetailedContinuousProfilerResponseFormatter"/>). Swapping which
/// one <c>AgentManager</c> wires up is a one-line change once that section is decided.
/// </summary>
public interface IContinuousProfilerCommandResponseFormatter
{
    IDictionary<string, object> Format(ContinuousProfilingCommandResult result);
}
