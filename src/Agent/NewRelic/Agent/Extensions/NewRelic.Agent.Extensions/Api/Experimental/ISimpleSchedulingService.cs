// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Api.Experimental
{
    public interface ISimpleSchedulingService
    {
        void StartExecuteEvery(Action action, TimeSpan timeBetweenExecutions, TimeSpan? optionalInitialDelay = null);

        void StopExecuting(Action action);
    }
}
