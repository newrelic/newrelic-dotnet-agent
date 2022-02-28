// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System;
using System.Threading;

namespace MultiFunctionApplicationHelpers.Libraries
{
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

            // Get everything started up and time for initial Sample().
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }
    }
}
