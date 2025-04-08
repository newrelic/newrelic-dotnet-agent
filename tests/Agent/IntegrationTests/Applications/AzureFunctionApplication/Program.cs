// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var host = new HostBuilder()
            // the net481 and net8 target uses the "basic" azure function configuration
            // the net10 target uses the aspnetcore azure function configuration
#if NETFRAMEWORK || NET8_0
            .ConfigureFunctionsWorkerDefaults()
#elif NET9_0
            .ConfigureFunctionsWebApplication()
#endif
            .Build();

        await host.RunAsync();
    }
}
