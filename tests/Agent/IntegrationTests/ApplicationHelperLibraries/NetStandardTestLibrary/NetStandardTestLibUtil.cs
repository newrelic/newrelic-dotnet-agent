// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using NewRelic.Agent.IntegrationTests.ApplicationHelpers;

namespace NetStandardTestLibrary
{
    [Library]
    public class NetStandardTestLibUtil
    {
        /// <summary>
        /// The point of this library is to test whether the agent can instrument a .NET Standard library
        /// that does not have a reference to mscorlib within a .NET Framework application.
        /// </summary>
        /// <param name="count"></param>
        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Test(int count)
        {
            Console.WriteLine($"{count,10}:Something Great is about to happen");
            if (count > 0)
            {
                Test(count - 1);
            }
            else
            {
                Console.WriteLine("Boom, something great happened!");
            }

            return;
        }
    }
}
