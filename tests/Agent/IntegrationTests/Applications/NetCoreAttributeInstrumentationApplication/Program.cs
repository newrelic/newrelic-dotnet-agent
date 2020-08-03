// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace NetCoreAttributeInstrumentationApplication
{
    class Program
    {
        private static string Port;

        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var commandLine = string.Join(" ", args);

            var result = CommandLineParser.SplitCommandLineIntoArguments(commandLine, true);

            Port = result.First().Split('=')[1];

            // Create handle that RemoteApplication expects
            var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "app_server_wait_for_all_request_done_" + Port);

            CreatePidFile();

            // Excersise this application
            DoSomething();

            eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [NewRelic.Api.Agent.Transaction]
        static void DoSomething()
        {
            DoSomethingInside();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [NewRelic.Api.Agent.Trace]
        private static void DoSomethingInside()
        {
            Thread.Sleep(2000);
        }

        private static void CreatePidFile()
        {
            var pid = Process.GetCurrentProcess().Id;
            var applicationName = Path.GetFileNameWithoutExtension(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath) + ".exe";
            var applicationDirectory =
                Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath),
                    applicationName);
            var pidFilePath = applicationDirectory + ".pid";
            var file = File.CreateText(pidFilePath);
            file.WriteLine(pid);
        }
    }
}
