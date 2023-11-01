// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using CommandLine;

namespace NewRelic.Agent.IntegrationTests.Applications.ApiAppNameChange
{
    public class Program
    {
        [Option("port", Required = true)]
        public string Port { get; set; }

        static void Main(string[] args)
        {
            if (Parser.Default == null)
                throw new NullReferenceException("CommandLine.Parser.Default");

            var program = new Program();
            if (!Parser.Default.ParseArgumentsStrict(args, program))
                return;

            // Create handle that RemoteApplication expects
            using (var eventWaitHandle =
                   new EventWaitHandle(false, EventResetMode.ManualReset,
                       "app_server_wait_for_all_request_done_" + program.Port))
            {

                CreatePidFile();

                Api.Agent.NewRelic.SetApplicationName("AgentApi");
                Api.Agent.NewRelic.StartAgent();
                Api.Agent.NewRelic.SetApplicationName("AgentApi2");

                // Wait for the test harness to tell us to shut down
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
            }
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
