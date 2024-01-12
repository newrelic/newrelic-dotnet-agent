// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using NewRelic.Api.Agent;

namespace ConsoleInstrumentationStartup;

static class Program
{
    [Transaction]
    static void Main()
    {
        Console.WriteLine($"{DateTime.Now} Main enter");

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        NewRelic.Api.Agent.NewRelic.SetTransactionName("category", "name");

        stopWatch.Stop();

        Console.WriteLine($"{DateTime.Now} Main exit");
    }
}
