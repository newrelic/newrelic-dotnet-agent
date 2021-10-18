// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Runtime.CompilerServices;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Internal
{
    [Library]
    internal static class AttributeInstrumentation
    {
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void DoSomething()
        {
            DoSomethingInside();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        [Trace]
        private static void DoSomethingInside()
        {
            System.Threading.Thread.Sleep(2000);
        }

    }
}
