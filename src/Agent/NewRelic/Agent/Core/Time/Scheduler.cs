using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using MoreLinq;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Collections;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Time
{
    public class Scheduler : IScheduler, IDisposable
    {
        // The System.Threading.Timer class uses -1 milliseconds as a magic "off" value
        private static readonly TimeSpan DisablePeriodicExecution = TimeSpan.FromMilliseconds(-1);

        [NotNull]
        private readonly Object _lock = new Object();

        [NotNull]
        private readonly DisposableCollection<TimerStatus> _oneTimeTimers = new DisposableCollection<TimerStatus>();

        [NotNull]
        private readonly IDictionary<Action, Timer> _recurringTimers = new Dictionary<Action, Timer>();

        #region Public API

        public void ExecuteOnce(Action action, TimeSpan timeUntilExecution)
        {
            if (timeUntilExecution < TimeSpan.Zero)
                throw new ArgumentException("Must be non-negative", "timeUntilExecution");

            lock (_lock)
            {
                // Clear out timers that have already executed
                _oneTimeTimers
                    .Where(execution => execution != null)
                    .Where(execution => execution.HasRun)
                    .ToList()
                    .ForEach(execution => _oneTimeTimers.Remove(execution));

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

        /// <summary>
        /// Schedules <paramref name="action"/> to execute asynchronously a single time after waiting for <paramref name="timeUntilExecution"/>.
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="timeUntilExecution">The delay until execution. Must be non-negative.</param>
        public static Timer CreateExecuteOnceTimer(Action action, TimeSpan timeUntilExecution)
        {
            if (timeUntilExecution < TimeSpan.Zero)
                throw new ArgumentException("Must be non-negative", "timeUntilExecution");

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

                    timer.Change(timeBetweenExecutions, DisablePeriodicExecution);
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
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

                _recurringTimers.Values
                    .Where(timer => timer != null)
                    .ForEach(timer => timer.Dispose());
                _recurringTimers.Clear();
            }
        }

        private class TimerStatus : IDisposable
        {
            [CanBeNull, UsedImplicitly]
            public Timer Timer;
            public Boolean HasRun;

            public void Dispose()
            {
                var timer = Timer;
                if (timer != null)
                    timer.Dispose();
            }
        }
    }
}
