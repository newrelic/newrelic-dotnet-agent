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

        [LibraryMethod]
        public static void StartAgent()
        {
            NewRelic.Api.Agent.NewRelic.StartAgent();
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
