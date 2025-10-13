// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Diagnostics;
using System.Net;
using System.Threading;
using ApplicationLifecycle;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AspNetCoreBasicWebApiApplication
{
    public class Program
    {
        private static string _port;

        public static void Main(string[] args)
        {
            _port = AppLifecycleManager.GetPortFromArgs(args);

            Activity.DefaultIdFormat = ActivityIdFormat.W3C;

            var ct = new CancellationTokenSource();
            var host = BuildHost(args);
            var task = host.RunAsync(ct.Token);

            AppLifecycleManager.CreatePidFile();

            AppLifecycleManager.WaitForTestCompletion(_port);

            ct.Cancel();

            task.GetAwaiter().GetResult();
        }

        public static IHost BuildHost(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseStartup<Startup>()
                        .UseUrls($@"http://127.0.0.1:{_port}/");
                })
                .Build();
    }
}
