// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.CodeAnalysis;

namespace AspNetCoreMvcBasicRequestsApplication
{
    public class Program
    {
        private const string DefaultPort = "5001";

        private static string _port;

        private static string _applicationName;

        public static void Main(string[] args)
        {
            var commandLine = string.Join(" ", args);

            _applicationName = Path.GetFileNameWithoutExtension(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath) + ".exe";

            Console.WriteLine($"[{_applicationName}] Joined args: {commandLine}");

            var result = CommandLineParser.SplitCommandLineIntoArguments(commandLine, true);

            var argPort = result.FirstOrDefault()?.Split('=')[1];
            _port = argPort ?? DefaultPort;

            Console.WriteLine($"[{_applicationName}] Received port: {argPort} | Using port: {_port}");

            OverrideSslSettingsForMockNewRelic();

            var ct = new CancellationTokenSource();
            var task = BuildWebHost(args).RunAsync(ct.Token);

            var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "app_server_wait_for_all_request_done_" + _port);
            CreatePidFile();
            eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));

            ct.Cancel();

            task.GetAwaiter().GetResult();
        }

        private static void CreatePidFile()
        {
            var pid = Process.GetCurrentProcess().Id;
            var applicationDirectory =
                Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath),
                    _applicationName);
            var pidFilePath = applicationDirectory + ".pid";

            using (var file = File.CreateText(pidFilePath))
            {
                file.WriteLine(pid);
            }

        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseUrls($@"http://localhost:{_port}/")
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
