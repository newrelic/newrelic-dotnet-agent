// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace ApplicationLifecycle
{
    public class Options
    {
        [Option('p', "port", Required = false, HelpText = "TCP port app should listen on")]
        public string Port { get; set; }
    }

    public class AppLifecycleManager
    {
        private const string ShutdownChannelPrefix = "app_server_wait_for_all_request_done_";

        private const string DefaultPort = "5001";

        private const int MinutesToWait = 5;

        private static string _applicationName;

        private static readonly bool _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        private static string ApplicationName
        {
            get
            {
                return _applicationName ?? (_applicationName = Path.GetFileNameWithoutExtension(new Uri(Assembly.GetEntryAssembly().CodeBase).LocalPath)
                + (_isLinux ? string.Empty : ".exe"));
            }
        }

        public static string GetPortFromArgs(string[] args)
        {
            var portToUse = DefaultPort;

            var commandLine = string.Join(" ", args);
            Log($"Joined args: {commandLine}");

            new Parser(with => { with.IgnoreUnknownArguments = true;})
                .ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    portToUse = o.Port ?? DefaultPort;
                    Log($"Received port: {o.Port} | Using port: {portToUse}");
                });

            return portToUse;
        }

        public static void WaitForTestCompletion(string port)
        {
            // On Linux, we have to used named pipes as the IPC mechanism because named EventWaitHandles aren't supported
            if (_isLinux)
            {
                using (NamedPipeServerStream pipeServer =
                new NamedPipeServerStream(ShutdownChannelPrefix + port, PipeDirection.In))
                {
                    var task = pipeServer.WaitForConnectionAsync();
                    if (task.Wait(TimeSpan.FromMinutes(MinutesToWait)))
                    {
                        try
                        {
                            // Read user input and send that to the client process.
                            using (StreamReader sr = new StreamReader(pipeServer))
                            {
                                string temp;
                                while ((temp = sr.ReadLine()) != null)
                                {
                                    Log($"Received shutdown message from test framework: {temp}");
                                }
                            }
                        }
                        // Catch the IOException that is raised if the pipe is broken
                        // or disconnected.
                        catch (IOException e)
                        {
                            Log($"WaitForTestCompletion: exception: {e.Message}");
                        }
                    }
                    else
                    {
                        Log("Timed out waiting for test completion.");
                    }
                }
            }
            else
            {
                try
                {
                    using (var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, ShutdownChannelPrefix + port))
                    {
                        if (!eventWaitHandle.WaitOne(TimeSpan.FromMinutes(MinutesToWait)))
                            Log("Timed out waiting for shutdown event handle to be signaled.");
                    }
                }
                catch (Exception e)
                {
                    Log("WaitForTestCompletion: exception: " + e.Message);
                }
            }
        }

        public static void CreatePidFile()
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

        private static void Log(string message)
        {
            Console.WriteLine($"[{ApplicationName}] {message}");
        }
    }
}
