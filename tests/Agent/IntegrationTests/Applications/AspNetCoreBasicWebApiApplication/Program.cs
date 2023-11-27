// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Diagnostics;
using System.Net;
using System.Threading;
using ApplicationLifecycle;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace AspNetCoreBasicWebApiApplication
{
    public class Program
    {
        private static string _port;

        public static void Main(string[] args)
        {
            _port = AppLifecycleManager.GetPortFromArgs(args);

            OverrideSslSettingsForMockNewRelic();

            Activity.DefaultIdFormat = ActivityIdFormat.W3C;

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

        /// <summary>
        /// When the MockNewRelic app is used in place of the normal New Relic / Collector endpoints,
        /// the mock version uses a self-signed cert that will not be "trusted."
        ///
        /// This forces all validation checks to pass.
        /// </summary>
        private static void OverrideSslSettingsForMockNewRelic()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate
            {
                //force trust on all certificates for simplicity
                return true;
            };
        }


    }
}
