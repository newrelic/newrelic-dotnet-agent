// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilities;
using NewRelic.Collections;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NewRelic.Agent.Core.Time
{
    public class Scheduler : IScheduler, IDisposable
    {
        // The System.Threading.Timer class uses -1 milliseconds as a magic "off" value
        private static readonly TimeSpan DisablePeriodicExecution = TimeSpan.FromMilliseconds(-1);

        private readonly object _lock = new object();

        private readonly DisposableCollection<TimerStatus> _oneTimeTimers = new DisposableCollection<TimerStatus>();

        private readonly IDictionary<Action, Timer> _recurringTimers = new Dictionary<Action, Timer>();

        #region Public API

        public void ExecuteOnce(Action action, TimeSpan timeUntilExecution)
        {
            if (timeUntilExecution < TimeSpan.Zero)
                throw new ArgumentException("Must be non-negative", "timeUntilExecution");

            lock (_lock)
            {
                // Removes processed oneTimeTimers
                var timersToRemove = new List<TimerStatus>();
                for (var x = 0; x < _oneTimeTimers.Count; x++)
                {
                    var execution = _oneTimeTimers[x];
                    if (execution.HasRun)
                    {
                        timersToRemove.Add(execution);
                    }
                }
                foreach (var ttr in timersToRemove)
                {
                    _oneTimeTimers.Remove(ttr);
                }

                // Create a new timer that will mark itself as complete after it executes. The timer needs to be stored somewhere so that it won't be GC'd before it runs.
                var newExecution = new TimerStatus();
                var flaggingAction = new Action(() =>
                {
                    action();
                    newExecution.HasRun = true;
                });
                var timer = CreateExecuteOnceTimer(flaggingAction, timeUntilExecution);
                newExecution.Timer = timer;

                _oneTimeTimers.Add(newExecution);
            }
        }

        public void ExecuteEvery(Action action, TimeSpan timeBetweenExecutions, TimeSpan? optionalInitialDelay = null)
        {
            if (timeBetweenExecutions < TimeSpan.Zero)
                throw new ArgumentException("Must be non-negative", "timeBetweenExecutions");

            lock (_lock)
            {
                var existingTimer = _recurringTimers.GetValueOrDefault(action);
                if (existingTimer != null)
                {
                    Log.Debug("Stopping existing timer for scheduled action");
                    existingTimer.Dispose();
                }

                var timer = CreateExecuteEveryTimer(action, timeBetweenExecutions, optionalInitialDelay);
                _recurringTimers[action] = timer;
            }
        }

        public static Timer CreateExecuteOnceTimer(Action action)
        {
            return PrivateCreateExecuteOnceTimer(action, DisablePeriodicExecution);
        }

        /// <summary>
        /// Schedules <paramref name="action"/> to execute asynchronously a single time after waiting for <paramref name="timeUntilExecution"/>.
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="timeUntilExecution">The delay until execution. Must be non-negative.</param>
        public static Timer CreateExecuteOnceTimer(Action action, TimeSpan timeUntilExecution)
        {
            if (timeUntilExecution < TimeSpan.Zero)
                throw new ArgumentException("Must be non-negative", "timeUntilExecution");

            return PrivateCreateExecuteOnceTimer(action, timeUntilExecution);
        }

        private static Timer PrivateCreateExecuteOnceTimer(Action action, TimeSpan timeUntilExecution)
        {
            var ignoreWorkAction = new TimerCallback(_ =>
            {
                using (new IgnoreWork())
                    action.CatchAndLog();
            });
            return new Timer(ignoreWorkAction, null, timeUntilExecution, DisablePeriodicExecution);
        }

        /// <summary>
        /// Create a timer that will execute <paramref name="action"/> asynchronously once per <paramref name="timeBetweenExecutions"/>. First execution is delayed until <paramref name="optionalInitialDelay"/>.
        /// 
        /// The timer will be paused while the action is executing.
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="timeBetweenExecutions">The delay until execution and between executions. Must be non-negative.</param>
        /// <param name="optionalInitialDelay">A specific time delay before the first execution. Must be non-negative. Defaults to <paramref name="timeBetweenExecutions"/> if unspecified.</param>
        public static Timer CreateExecuteEveryTimer(Action action, TimeSpan timeBetweenExecutions, TimeSpan? optionalInitialDelay = null)
        {
            var initialDelay = optionalInitialDelay ?? timeBetweenExecutions;

            if (timeBetweenExecutions < TimeSpan.Zero)
                throw new ArgumentException("Must be non-negative", "timeBetweenExecutions");
            if (initialDelay < TimeSpan.Zero)
                throw new ArgumentException("Must be non-negative", "optionalInitialDelay");

            var timer = null as Timer;
            var ignoreWorkAction = new TimerCallback(_ =>
            {
                try
                {
                    timer.Change(DisablePeriodicExecution, DisablePeriodicExecution);

                    using (new IgnoreWork())
                        action();

                }
                catch (Exception exception)
                {
                    Log.Error(exception, "CreateExecuteEveryTimer() failed");
                }
                finally
                {
                    try
                    {
                        // Change timer in finally so its enabled even if there was an exception
                        // while executing Action. 
                        timer.Change(timeBetweenExecutions, DisablePeriodicExecution);
                    }
                    catch (ObjectDisposedException)
                    {
                        // This can happen when the agent shuts down. The callback for a timer can still
                        // called after the timer has been disposed because it was already queue up. Even
                        // if you used the other Dispose overload that will attempt to wait for the callback
                        // to complete the documentation mentions that there is a race condition that could
                        // allow the callback to still execute. This catch block is here to prevent the
                        // instrumented application from crashing.
                    }
                }

            });

            // Initialize the timer with an execution time of Never so that we can guarantee `timer` is assign before the timer ticks the first time
            timer = new Timer(ignoreWorkAction, null, DisablePeriodicExecution, DisablePeriodicExecution);
            timer.Change(initialDelay, DisablePeriodicExecution);

            return timer;
        }

        public void StopExecuting(Action action, TimeSpan? timeToWaitForInProgressAction = null)
        {
            lock (_lock)
            {
                var existingTimer = _recurringTimers.GetValueOrDefault(action);
                if (existingTimer == null)
                    return;

                _recurringTimers.Remove(action);

                if (timeToWaitForInProgressAction == null)
                {
                    existingTimer.Dispose();
                }
                else
                {
                    var waitHandle = new ManualResetEvent(false);
                    existingTimer.Dispose(waitHandle);

                    // We intentionally allow timeout exceptions to bubble up
                    waitHandle.WaitOne(timeToWaitForInProgressAction.Value);
                }
            }
        }

        #endregion Public API

        public void Dispose()
        {
            lock (_lock)
            {
                _oneTimeTimers.Dispose();

                foreach (var timer in _recurringTimers.Values)
                {
                    if (timer != null)
                    {
                        timer.Dispose();
                    }
                }
                _recurringTimers.Clear();
            }
        }

        private class TimerStatus : IDisposable
        {
            public Timer Timer;
            public bool HasRun;

            public void Dispose()
            {
                var timer = Timer;
                if (timer != null)
                    timer.Dispose();
            }
        }
    }
}
