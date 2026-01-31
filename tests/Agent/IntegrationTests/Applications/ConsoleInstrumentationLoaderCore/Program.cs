// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Runtime.CompilerServices;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace ConsoleInstrumentationLoaderCore;

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