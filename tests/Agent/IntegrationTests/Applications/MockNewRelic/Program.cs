// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using ApplicationLifecycle;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace MockNewRelic
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
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, int.Parse(_port), listenOptions =>
                    {
                        listenOptions.UseHttps(new HttpsConnectionAdapterOptions()
                        {
                            ServerCertificate = new X509Certificate2("testcert.pfx", "password1"),
                            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls
                        });
                    });
                })
                .Build();

    }
}
