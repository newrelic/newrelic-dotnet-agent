// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using ApplicationLifecycle;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AspNetCoreMvcBasicRequestsApplication
{
    public class Program
    {
        private static string _port;

        public static void Main(string[] args)
        {
            _port = AppLifecycleManager.GetPortFromArgs(args);

            OverrideSslSettingsForMockNewRelic();

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

        /// <summary>
        /// When the MockNewRelic app is used in place of the normal New Relic / Collector endpoints,
        /// the mock version uses a self-signed cert that will not be "trusted."
        ///
        /// This forces all validation checks to pass.
        /// </summary>
        private static void OverrideSslSettingsForMockNewRelic()
        {
#if !NET10_0_OR_GREATER
            ServicePointManager.ServerCertificateValidationCallback = delegate
            {
                //force trust on all certificates for simplicity
                return true;
            };
#endif
        }
    }
}
