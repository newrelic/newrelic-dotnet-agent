// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Command-driven control surface for continuous profiling, implemented by
/// <see cref="ContinuousProfilingService"/>. Separate from <see cref="IContinuousProfilingSessionControl"/>
/// (which the thread profiler uses purely to read <c>IsActive</c> for mutual exclusion) because
/// command-driven start/stop carries its own parameters and its own idempotent-no-op / exception-reporting
/// contract, per the start_continuous_profiler/stop_continuous_profiler agent command spec.
/// </summary>
public interface IContinuousProfilingCommandTarget
{
    /// <summary>A type started via this method stays under command ownership until a matching <see cref="StopFromCommand"/> or process restart.</summary>
    ContinuousProfilingCommandResult StartFromCommand(IReadOnlyList<string> requestedTypes, int? sampleIntervalMs, int? cpuReportIntervalMs);

    ContinuousProfilingCommandResult StopFromCommand(IReadOnlyList<string> requestedTypes);
}
