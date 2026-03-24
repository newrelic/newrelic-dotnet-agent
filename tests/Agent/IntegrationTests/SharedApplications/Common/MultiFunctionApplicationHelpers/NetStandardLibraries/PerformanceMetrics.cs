// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Threading;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.Libraries;

[Library]
public static class PerformanceMetrics
{
    [LibraryMethod]
    public static void Test(int countMaxWorkerThreads, int countMaxCompletionThreads)
    {
        ConsoleMFLogger.Info($"Setting Threadpool Max Threads: {countMaxWorkerThreads} worker/{countMaxCompletionThreads} completion.");

        ThreadPool.SetMaxThreads(countMaxWorkerThreads, countMaxCompletionThreads);

        StartAgent();
    }

    /// <summary>
    /// This is an instrumented method that doesn't actually do anything.  Its purpose
    /// is to ensure that the agent starts up.  Without an instrumented method, the agent won't
    /// start.
    /// </summary>
    [Transaction]
    private static void StartAgent()
    {
        ConsoleMFLogger.Info("Instrumented Method to start the Agent");

        // We need atleast one known GC invocation to verify our GC metrics.
        GC.Collect();

        // Gen0-only collections ensure the GCSamplerV2's per-generation counting
        // (which subtracts higher-gen counts from lower-gen counts) reports non-zero
        // Gen0 collections. Without these, a full GC.Collect() increments all generation
        // counters equally, and the subtraction can yield zero on fast-starting processes.
        GC.Collect(0);
        GC.Collect(0);

        // Get everything started up and time for initial Sample().
        Thread.Sleep(TimeSpan.FromSeconds(10));
    }
}