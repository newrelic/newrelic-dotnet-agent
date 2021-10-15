// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.Threading;
using System.Reflection;

namespace AspNetCoreWebApiCustomAttributesApplication
{
    public class Program
    {
        private static string _port;

        public static void Main(string[] args)
        {
            var ct = new CancellationTokenSource();
            var task = BuildWebHost(args).RunAsync(ct.Token);

            ct.Cancel();

            task.GetAwaiter().GetResult();
        }


        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseUrls(string.Format(@"http://localhost:{0}/", _port))
                .Build();
    }
}
