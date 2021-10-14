// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
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

        private static readonly bool _isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);

        public static void Main(string[] args)
        {
            var commandLine = string.Join(" ", args);

            _applicationName = Path.GetFileNameWithoutExtension(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath)
                + (_isLinux ? string.Empty : ".exe");

            Console.WriteLine($"[{_applicationName}] Joined args: {commandLine}");

            var result = CommandLineParser.SplitCommandLineIntoArguments(commandLine, true);

            var argPort = result.FirstOrDefault()?.Split('=')[1];
            _port = argPort ?? DefaultPort;

            Console.WriteLine($"[{_applicationName}] Received port: {argPort} | Using port: {_port}");

            OverrideSslSettingsForMockNewRelic();

            var ct = new CancellationTokenSource();
            var task = BuildWebHost(args).RunAsync(ct.Token);

            CreatePidFile();

            WaitForTestCompletion();

            ct.Cancel();

            task.GetAwaiter().GetResult();
        }

        private static void WaitForTestCompletion()
        {
            var minutesToWait = 5;
            var shutdownChannelName = "app_server_wait_for_all_request_done_" + _port;

            // On Linux, we have to used named pipes as the IPC mechanism because named EventWaitHandles aren't supported
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                using (NamedPipeServerStream pipeServer =
                new NamedPipeServerStream(shutdownChannelName, PipeDirection.In))
                {
                    var task = pipeServer.WaitForConnectionAsync();
                    if (task.Wait(TimeSpan.FromMinutes(minutesToWait)))
                    {
                        try
                        {
                            // Read user input and send that to the client process.
                            using (StreamReader sr = new StreamReader(pipeServer))
                            {
                                string temp;
                                while ((temp = sr.ReadLine()) != null)
                                {
                                    Console.WriteLine("Received from client: {0}", temp);
                                }
                            }
                        }
                        // Catch the IOException that is raised if the pipe is broken
                        // or disconnected.
                        catch (IOException e)
                        {
                            Console.WriteLine("ERROR: {0}", e.Message);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Timed out waiting for test completion.");
                    }
                }
            }
            else
            {
                var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, shutdownChannelName);
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(minutesToWait));
            }
        }

        private static void CreatePidFile()
        {
            var pid = Process.GetCurrentProcess().Id;
            Console.WriteLine($"Pid={pid}");
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
