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
    /// <summary>
    /// Starts (or no-ops if already running) continuous profiling for the requested "include" tokens
    /// ("all", "cpu", "heap"), or reports current status unchanged if <paramref name="requestedTypes"/> is
    /// empty. Once a type is started via this method it stays under command ownership -- a later
    /// config-driven <see cref="ContinuousProfilingService.ApplyConfigChange"/> will not stop it -- until a
    /// matching <see cref="StopFromCommand"/> call or process restart.
    /// </summary>
    ContinuousProfilingCommandResult StartFromCommand(IReadOnlyList<string> requestedTypes, int? sampleIntervalMs, int? cpuReportIntervalMs);

    /// <summary>
    /// Stops (or no-ops if not running) continuous profiling for the requested "include" tokens, or reports
    /// current status unchanged if <paramref name="requestedTypes"/> is empty. Releases command ownership
    /// of the requested types back to config control.
    /// </summary>
    ContinuousProfilingCommandResult StopFromCommand(IReadOnlyList<string> requestedTypes);
}
