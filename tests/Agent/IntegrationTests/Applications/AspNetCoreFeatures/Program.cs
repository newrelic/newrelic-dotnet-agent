// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Threading;
using ApplicationLifecycle;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AspNetCoreFeatures
{
    public class Program
    {
        private static string _port;

        public static void Main(string[] args)
        {
            _port = AppLifecycleManager.GetPortFromArgs(args);

            var ct = new CancellationTokenSource();
            var task = CreateHostBuilder(args).Build().RunAsync(ct.Token);

            AppLifecycleManager.CreatePidFile();

            AppLifecycleManager.WaitForTestCompletion(_port);

            ct.Cancel();

            task.GetAwaiter().GetResult();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseStartup<Startup>()
                        .UseUrls($@"http://127.0.0.1:{_port}/");
                });

    }
}
