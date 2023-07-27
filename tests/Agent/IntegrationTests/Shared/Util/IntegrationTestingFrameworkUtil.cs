// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace NewRelic.Agent.IntegrationTests.Shared.Util
{
    /// <summary>
    /// Helper class for testing applications to interact with the
    /// Integration Testing Framework
    /// </summary>
    public static class IntegrationTestingFrameworkUtil
    {

        /// <summary>
        /// Caller must dispose eventWaitHandle!
        /// </summary>
        /// <param name="args"></param>
        /// <param name="defaultPort"></param>
        /// <param name="eventWaitHandle"></param>
        /// <param name="cancellationTokenSource"></param>
        /// <param name="initializationAction"></param>
        public static void RegisterProcessWithTestFrameworkAndInitialize(string[] args, string defaultPort, out EventWaitHandle eventWaitHandle, out CancellationTokenSource cancellationTokenSource, Action<string[], CancellationTokenSource, string> initializationAction)
        {
            var applicationName = Path.GetFileNameWithoutExtension(new Uri(Assembly.GetEntryAssembly().Location).LocalPath) + ".exe";

            Console.WriteLine($"[{applicationName}] Invoked with args: { string.Join(" ", args) }");

            var port = GetPortFromArgs(args) ?? defaultPort;

            Console.WriteLine($"[{applicationName}] Parsed port: { port }");

            cancellationTokenSource = new CancellationTokenSource();

            var eventWaitHandleName = "app_server_wait_for_all_request_done_" + port;

            Console.WriteLine($"[{applicationName}] Setting EventWaitHandle name to: { eventWaitHandleName }");

            eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventWaitHandleName);

            initializationAction(args, cancellationTokenSource, port);

            CreatePidFile(applicationName);
        }

        public static void CreatePidFile(string applicationName)
        {
            var pid = Process.GetCurrentProcess().Id;
            var applicationDirectory =
                Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().Location).LocalPath),
                    applicationName);
            var pidFilePath = applicationDirectory + ".pid";
            var file = File.CreateText(pidFilePath);
            file.WriteLine(pid);
        }


        public static string GetPortFromArgs(string[] args)
        {
            return GetValueFromArgs(args, "port");
        }

        public static string GetValueFromArgs(string[] args, string argName)
        {
            argName = argName.ToLower();
            for (var i = 0; i < args.Length; i++)
            {
                var argValue = args[i].ToLower();

                var isSingleArg = argValue.StartsWith($"--{argName}=");
                if (isSingleArg)
                {
                    var val = argValue.Split('=')[1];
                    return val.Trim();
                }

                var valueInFollowingArg = argValue.EndsWith(argName);
                if (valueInFollowingArg)
                {
                    var nextIndex = i + 1;
                    if (nextIndex >= args.Length)
                    {
                        throw new ArgumentException($"No value specified for {argName}");
                    }

                    var val = args[nextIndex];
                    return val.Trim();
                }
            }

            return null;
        }
    }
}
