// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var host = new HostBuilder()
            // the net481 and net10 target uses the "basic" azure function configuration
            // the net11 target uses the aspnetcore azure function configuration
#if NETFRAMEWORK || NET10_0
            .ConfigureFunctionsWorkerDefaults()
#elif NET11_0 
            .ConfigureFunctionsWebApplication()
#endif
            .Build();

        await host.RunAsync();
    }
}
