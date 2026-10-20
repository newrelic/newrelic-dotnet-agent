// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Time;

public interface IScheduler
{
    /// <summary>
    /// Schedules <paramref name="action"/> to execute asynchronously a single time after waiting for <paramref name="timeUntilExecution"/>.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="timeUntilExecution">The delay until execution. Must be non-negative.</param>
    /// <param name="trackAsAgentWork">When true (default), the calling thread is marked as agent-owned
    /// background dispatch (native thread-identity signal, see follow-up #16) for the duration of
    /// <paramref name="action"/>. Pass false for an action that itself reads the continuous-profiling
    /// native sample pipeline (e.g. its own drain) -- marking that thread poisons its own read.</param>
    void ExecuteOnce(Action action, TimeSpan timeUntilExecution, bool trackAsAgentWork = true);

    /// <summary>
    /// Schedules <paramref name="action"/> to execute asynchronously once per <paramref name="timeBetweenExecutions"/>. First execution is delayed until <paramref name="optionalInitialDelay"/>.
    ///
    /// The timer will be paused while the action is executing.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="timeBetweenExecutions">The delay until execution and between executions. Must be non-negative.</param>
    /// <param name="optionalInitialDelay">A specific time delay before the first execution. Must be non-negative. Defaults to <paramref name="timeBetweenExecutions"/> if unspecified.</param>
    /// <param name="trackAsAgentWork">When true (default), the calling thread is marked as agent-owned
    /// background dispatch (native thread-identity signal, see follow-up #16) for the duration of
    /// <paramref name="action"/>. Pass false for an action that itself reads the continuous-profiling
    /// native sample pipeline (e.g. its own drain) -- marking that thread poisons its own read.</param>
    void ExecuteEvery(Action action, TimeSpan timeBetweenExecutions, TimeSpan? optionalInitialDelay = null, bool trackAsAgentWork = true);

    /// <summary>
    /// Removes any scheduled recurrences of <paramref name="action"/>. Will not stop an action that has been scheduled via <see cref="ExecuteOnce"/>. If the action is currently executing and <paramref name="timeToWaitForInProgressAction"/> is not null, will block until the action is finished or throw if the timeout is reached.
    /// </summary>
    /// <param name="action">The action to stop executing repeatedly.</param>
    /// <param name="timeToWaitForInProgressAction"></param>
    void StopExecuting(Action action, TimeSpan? timeToWaitForInProgressAction = null);
}