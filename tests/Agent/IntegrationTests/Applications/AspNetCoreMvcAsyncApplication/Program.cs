// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Threading;
using ApplicationLifecycle;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace AspNetCoreMvcAsyncApplication
{
    public class Program
    {
        private static string _port;

        public static void Main(string[] args)
        {
            Thread.CurrentThread.Name = "NewRelic Main Test Application Thread";

            _port = AppLifecycleManager.GetPortFromArgs(args);


            var ct = new CancellationTokenSource();
            var task = BuildWebHost(args).RunAsync(ct.Token);

            AppLifecycleManager.CreatePidFile();

            AppLifecycleManager.WaitForTestCompletion(_port);

            ct.Cancel();

            task.GetAwaiter().GetResult();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseUrls($@"http://127.0.0.1:{_port}/")
                .Build();

    }
}
