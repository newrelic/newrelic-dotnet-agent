// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers
{
    [Library]
    public static class RootCommands
    {
        [LibraryMethod]
        public static void DelaySeconds(int seconds)
        {
            Task.Delay(TimeSpan.FromSeconds(seconds)).Wait();
        }

        [LibraryMethod]
        public static void LaunchDebugger()
        {
            Debugger.Launch();
        }

        [LibraryMethod]
        [Transaction]
        public static void InstrumentedMethodToStartAgent()
        {
            // Mission accomplished
        }
    }
}
