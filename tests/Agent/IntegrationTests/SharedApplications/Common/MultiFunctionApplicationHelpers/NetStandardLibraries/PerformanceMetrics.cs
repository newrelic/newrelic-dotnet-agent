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

        // Induce Gen0-only collections BEFORE the agent starts. GCSamplerV2 reads
        // cumulative GC.CollectionCount() values, so these pre-agent collections
        // guarantee that CollectionCount(0) > CollectionCount(1) by the time the
        // first sample runs — regardless of when the sampler fires relative to
        // StartAgent(). V1 (EventListener-based) is unaffected since it only
        // captures events after the listener registers inside StartAgent().
        GC.Collect(0);
        GC.Collect(0);

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

        // GCSamplerV2 computes Gen0-only collections as: raw Gen0 count - raw Gen1 count
        // (see ImmutableGCSample). The full GC.Collect() above increments both Gen0 and
        // Gen1 raw counters by 1, so the Gen0-only count is 1 - 1 = 0. These two
        // Gen0-only collections each increment the raw Gen0 counter without affecting
        // Gen1, bringing it to (1+2) - 1 = 2 and ensuring the test sees a non-zero
        // Gen0 metric.
        GC.Collect(0);
        GC.Collect(0);

        // Get everything started up and time for initial Sample().
        Thread.Sleep(TimeSpan.FromSeconds(10));
    }
}