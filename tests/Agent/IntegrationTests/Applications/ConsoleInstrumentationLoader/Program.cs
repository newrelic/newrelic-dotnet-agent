// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Api.Agent;
using System;
using System.Runtime.CompilerServices;
using NewRelic.Agent.IntegrationTests.ApplicationHelpers;

namespace ConsoleInstrumentationLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            Environment.ExitCode = -1;
            try
            {
                InstrumentedMethod();

                ReflectionUtil.ScanAssembliesAndTypes();
            }
            catch (Exception)
            {
                Environment.ExitCode = -2;
                throw;
            }

            Environment.ExitCode = 0;
        }

        [Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InstrumentedMethod()
        {
            Console.WriteLine("Instrumented Method");
        }
    }
}
