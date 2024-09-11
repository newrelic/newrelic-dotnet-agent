// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ApplicationLifecycle;
using Microsoft.Extensions.Hosting;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        _ = AppLifecycleManager.GetPortFromArgs(args);

        // --port arg to this app is different from the --port arg sent in the `func` invocation
        // so we have to pull it from a custom environment variable
        var port = Environment.GetEnvironmentVariable("AZURE_FUNCTION_APP_EVENT_HANDLE_PORT");
        if (string.IsNullOrEmpty(port))
            throw new Exception("AZURE_FUNCTION_APP_EVENT_HANDLE_PORT environment variable not set");


        var host = new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .Build();

        var task = host.RunAsync(cts.Token);

        AppLifecycleManager.CreatePidFile();
        AppLifecycleManager.WaitForTestCompletion(port);

        cts.Cancel();
        await task;
    }
}
