// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Time
{
    public class SimpleSchedulingService : DisposableService, ISimpleSchedulingService
    {
        private readonly IScheduler _scheduler;
        private readonly List<Action> _executingActions;

        public SimpleSchedulingService(IScheduler scheduler)
        {
            _scheduler = scheduler;
            _executingActions = new List<Action>();
        }

        public void StartExecuteEvery(Action action, TimeSpan timeBetweenExecutions, TimeSpan? optionalInitialDelay = null)
        {
            _scheduler.ExecuteEvery(action, timeBetweenExecutions, optionalInitialDelay);
            _executingActions.Add(action);
        }

        public void StopExecuting(Action action)
        {
            _scheduler.StopExecuting(action);
            _executingActions.Remove(action);
        }

        public override void Dispose()
        {
            foreach (var executingAction in _executingActions)
            {
                _scheduler.StopExecuting(executingAction);
            }

            _executingActions.Clear();

            base.Dispose();
        }
    }
}
