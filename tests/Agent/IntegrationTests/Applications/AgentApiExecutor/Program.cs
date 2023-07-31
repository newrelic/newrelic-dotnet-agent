// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using CommandLine;

namespace NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor
{
    public class Program
    {
        [Option("port", Required = true)]
        public string Port { get; set; }

        static void Main(string[] args)
        {
            RealMain(args);
            Thread.Sleep(1000); //needed for OtherTransaction test
        }

        static void RealMain(string[] args)
        {
            if (Parser.Default == null)
                throw new NullReferenceException("CommandLine.Parser.Default");

            var program = new Program();
            if (!Parser.Default.ParseArgumentsStrict(args, program))
                return;

            // Create handle that RemoteApplication expects
            Console.WriteLine("PID {0} Creating EventWaitHandle {1}", Process.GetCurrentProcess().Id, "app_server_wait_for_all_request_done_" + program.Port);
            using (var eventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset,
                       "app_server_wait_for_all_request_done_" + program.Port))
            {

                CreatePidFile();

                Api.Agent.NewRelic.StartAgent();

                SomeSlowMethod();

                Api.Agent.NewRelic.RecordMetric("MyMetric", 3.14159F);
                Api.Agent.NewRelic.NoticeError(new Exception("Rawr!"));

                var errorAttributes = new Dictionary<string, string> { { "hey", "dude" }, { "faz", "baz" }, };
                Api.Agent.NewRelic.NoticeError(new Exception("Rawr!"), errorAttributes);

                SomeOtherMethod();

                // Wait for the test harness to tell us to shut down
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SomeSlowMethod()
        {
            var stuff = string.Empty;
            Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("test", "test");
            Thread.Sleep(2000); //needed for OtherTransaction test
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SomeOtherMethod()
        {
            Thread.Sleep(20);
        }

        private static void CreatePidFile()
        {
            var pid = Process.GetCurrentProcess().Id;
            var thisAssemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var pidFilePath = thisAssemblyPath + ".pid";
            var file = File.CreateText(pidFilePath);
            file.WriteLine(pid);
        }
    }
}
