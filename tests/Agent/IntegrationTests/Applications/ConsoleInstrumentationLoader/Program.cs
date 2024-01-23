// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Api.Agent;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;

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
            catch (Exception ex)
            {
                if (ex is ReflectionTypeLoadException rex)
                {
                    var list = rex.LoaderExceptions.Select(x => x.Message).ToList();

                    Console.WriteLine($"ReflectionTypeLoadException.LoaderExceptions: {string.Join(Environment.NewLine, list)}");
                }
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
