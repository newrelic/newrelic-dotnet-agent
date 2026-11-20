// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core;

public static class ProfilerServicesTeardown
{
    // The continuous-profiling dispose must run even when the thread-profiling stop throws, or the CP native
    // sampler thread leaks. Kept out of AgentManager so that calling it does not run AgentManager's static
    // initializer, which starts a full agent.
    public static void Stop(Action stopThreadProfiler, Action disposeContinuousProfiler)
    {
        try
        {
            stopThreadProfiler?.Invoke();
        }
        finally
        {
            disposeContinuousProfiler?.Invoke();
        }
    }
}
