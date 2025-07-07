// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ThreadProfileStressTest
{
    class Program
    {
        private const string DefaultPort = "5001";
        private static string _applicationName;

        static void Main(string[] args)
        {
#if !NET9_0_OR_GREATER
            ServicePointManager.ServerCertificateValidationCallback = delegate
            {
                //force trust on all certificates for simplicity
                return true;
            };
#endif
            _applicationName =
                Path.GetFileNameWithoutExtension(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath) + ".exe";

            Console.WriteLine($"[{_applicationName}] Invoked with args: {string.Join(" ", args)}");

            var port = GetPortFromArgs(args) ?? DefaultPort;

            Console.WriteLine($"[{_applicationName}] Parsed port: {port}");

            var eventWaitHandleName = "app_server_wait_for_all_request_done_" + port;

            Console.WriteLine("[{0}] Setting EventWaitHandle name to: {1}", _applicationName, eventWaitHandleName);

            using (var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventWaitHandleName))
            {

                DoTheThing(port);

                //Give the application up to an additional 3 minutes before automatically terminating the app
                //so that there is enough time for the thread profiler session to complete and data to be sent
                //to the collector.
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(3));
            }
        }

        static void DoTheThing(string port)
        {
            //Do something to cause the agent to connect and start monitoring this process
            DoWork();

            //Wait to create the Pid file until the agent is triggered by the previous call to DoWork.
            //Otherwise the test framework could trigger the exercise app callback too soon and could crash before
            //the agent's log directory and corresponding log file is created.
            CreatePidFile();

            Console.WriteLine("[{0}] Waiting for signal to start the thread stress test.", _applicationName);

            var eventWaitHandleName = $"thread_profile_stress_begin_{port}";
            using (var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventWaitHandleName))
            {
                eventWaitHandle.WaitOne(TimeSpan.FromSeconds(90));
            }

            Console.WriteLine("[{0}] Starting the thread stress test.", _applicationName);

            //Create and Destroy threads often so that we are likely to encounter a situation where the thread profiler
            //will attempt to walk the stack of a thread that has already been destroyed. This is to help catch a problem
            //where we get a list of threads we want to take a snapshot of, and one of those threads is destroyed before
            //we get a chance to take its snapshot. Attempting to take a snapshot of a thread that's been destroyed
            //triggers a fatal error that causes the CLR to terminate the process.

            //Only hammer the system for 15s instead of the full thread profile duration of 2 minutes so that the agent's
            //threads have enough time to process everything it needs.
            var maxTimeToRun = TimeSpan.FromSeconds(15);
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < maxTimeToRun)
            {
                var thread = new Thread(DoWork) { IsBackground = true };
                thread.Start();
                Thread.Sleep(5);
            }

            sw.Stop();
            Console.WriteLine("[{0}] Stopping the thread stress test.", _applicationName);
        }

        [NewRelic.Api.Agent.Transaction(Web = false)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DoWork()
        {
            try
            {
                Method1();
            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }

        }

        [NewRelic.Api.Agent.Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Method1()
        {
            try
            {
                Method2();
            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }
        }

        [NewRelic.Api.Agent.Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Method2()
        {
            try
            {
                Method3();
            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }
        }

        [NewRelic.Api.Agent.Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Method3()
        {
        }

        private static void CreatePidFile()
        {
            var pid = Process.GetCurrentProcess().Id;
            var applicationDirectory =
                Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath),
                    _applicationName);
            var pidFilePath = applicationDirectory + ".pid";
            var file = File.CreateText(pidFilePath);
            file.WriteLine(pid);
        }

        private static string GetPortFromArgs(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var argValue = args[i].ToLower();

                var isPortSingleArg = argValue.StartsWith("--port=");
                if (isPortSingleArg)
                {
                    var portValue = argValue.Split('=')[1];
                    return portValue.Trim();
                }

                var isPort = argValue.EndsWith("port");
                if (isPort)
                {
                    var nextIndex = i + 1;
                    if (nextIndex >= args.Length)
                    {
                        throw new ArgumentException("No value specified for port");
                    }

                    var port = args[nextIndex];
                    return port.Trim();
                }
            }

            return null;
        }
    }
}
