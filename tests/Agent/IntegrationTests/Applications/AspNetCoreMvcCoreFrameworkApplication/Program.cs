// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NewRelic.Agent.IntegrationTests.Shared.Util;

namespace AspNetCoreMvcCoreFrameworkApplication
{
    public class Program
    {
        private const string DefaultPort = "5001";

        public static void Main(string[] args)
        {
            Task webHostTask = null;
            IntegrationTestingFrameworkUtil.RegisterProcessWithTestFrameworkAndInitialize(args, DefaultPort, out var eventWaitHandle, out var cancellationTokenSource, (allArgs, cts, port) => webHostTask = BuildWebHost(allArgs, port).RunAsync(cts.Token));
            using (eventWaitHandle)
            {
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
            }

            cancellationTokenSource.Cancel();

            webHostTask?.GetAwaiter().GetResult();
        }

        public static IWebHost BuildWebHost(string[] args, string port) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseUrls($@"http://127.0.0.1:{port}/")
                .Build();
    }
}
