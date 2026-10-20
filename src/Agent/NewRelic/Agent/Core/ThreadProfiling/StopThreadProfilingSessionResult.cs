// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ThreadProfiling;

/// <summary>
/// Outcome of a <see cref="IThreadProfilingSessionControl.StopThreadProfilingSession"/> request. Replaces
/// an earlier bool so the <c>stop_profiler</c> command can tell a genuinely-not-running session apart from
/// one whose sampling worker outran the bounded join and is still finishing (its collected data is still
/// being sent) -- both of which previously returned <c>false</c> and reported "not running" to the
/// collector even though the latter was mid-send.
/// </summary>
public enum StopThreadProfilingSessionResult
{
    /// <summary>No session matched the request: none was ever started, it already completed, or the
    /// requested profile id did not match the in-process session.</summary>
    NotRunning,

    /// <summary>The sampling worker was confirmed stopped; profile state and cache were reset.</summary>
    Stopped,

    /// <summary>The stop was requested, but the sampling worker outran the bounded join and is still
    /// winding down -- its already-collected profile data is still being sent. Profile state is left
    /// intact so the still-running worker's aggregation is not corrupted.</summary>
    StopRequestedWorkerStillFinishing
}
