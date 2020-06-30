/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using NewRelic.Agent.IntegrationTests.ApplicationHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    public static class FasterEventHarvest
    {
        [LibraryMethod]
        public static void Test()
        {
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
            Logger.Info("Instrumented Method to start the Agent");
        }
    }
}
