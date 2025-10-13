// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Threading;
using ApplicationLifecycle;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace MockNewRelic
{
    public class Program
    {
        private static string _port;

        public static void Main(string[] args)
        {
            _port = AppLifecycleManager.GetPortFromArgs(args);

            var ct = new CancellationTokenSource();
            var host = BuildHost(args);

            var runTask = host.RunAsync(ct.Token);

            AppLifecycleManager.CreatePidFile();

            AppLifecycleManager.WaitForTestCompletion(_port);

            ct.Cancel();

            runTask.GetAwaiter().GetResult();
        }

        public static IHost BuildHost(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseStartup<Startup>()
                        .ConfigureKestrel(options =>
                        {
                            options.Listen(IPAddress.Loopback, int.Parse(_port), listenOptions =>
                            {
                                listenOptions.UseHttps();
                            });
                        });
                })
                .Build();
    }
}
