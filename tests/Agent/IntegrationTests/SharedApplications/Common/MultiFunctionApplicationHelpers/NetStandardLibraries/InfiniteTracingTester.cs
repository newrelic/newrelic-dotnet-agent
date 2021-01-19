// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    public class InfiniteTracingTester
    {
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void Make8TSpan()
        {
            var rand = new Random(10);
            rand.Next();
            rand.Next();
            rand.Next();
            rand.Next();
        }

        /// <summary>
        /// This is an instrumented method that doesn't actually do anything.  Its purpose
        /// is to ensure that the agent starts up.  Without an instrumented method, the agent won't
        /// start.
        /// </summary>
        [LibraryMethod]
        [Transaction]
        public static void StartAgent()
        {
            Logger.Info("Instrumented Method to start the Agent");

            //Get everything started up and time for initial Sample().
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }

        [LibraryMethod]
        public static void Wait()
        {
            Thread.Sleep(TimeSpan.FromSeconds(70));
        }
    }
}
