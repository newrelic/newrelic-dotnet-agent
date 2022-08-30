// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System.Threading;
using ApplicationLifecycle;

namespace AspNetCoreWebApiCustomAttributesApplication
{
    public class Program
    {
        private static string _port;

        public static void Main(string[] args)
        {
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
                .UseUrls(string.Format(@"http://127.0.0.1:{0}/", _port))
                .Build();
    }
}
