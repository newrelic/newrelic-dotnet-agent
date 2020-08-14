// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using System.Threading.Tasks;

namespace NetCoreAsyncApplication
{
    class Program
    {
        private const string DefaultPort = "5001";

        private static string _port;

        private static string _applicationName;

        static void Main(string[] args)
        {
            var commandLine = string.Join(" ", args);

            _applicationName = Path.GetFileNameWithoutExtension(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath) + ".exe";

            Console.WriteLine($"[{_applicationName}] Joined args: {commandLine}");

            var result = CommandLineParser.SplitCommandLineIntoArguments(commandLine, true).ToList();

            var argPort = result.FirstOrDefault()?.Split('=')[1];
            _port = argPort ?? DefaultPort;

            Console.WriteLine($"[{_applicationName}] Received port: {argPort} | Using port: {_port}");

            var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "app_server_wait_for_all_request_done_" + _port);

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            var doWorkTask = Task.Run(async () => await DoWorkAsync(), cancellationTokenSource.Token);
            doWorkTask.Wait(cancellationTokenSource.Token);

            //Wait to create the Pid file until the work is done so that the agent has a chance to attach and create log directory and log files.
            CreatePidFile();

            eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
        }

        private static async Task DoWorkAsync()
        {
            var asyncUseCases = new AsyncUseCases();

            await asyncUseCases.IoBoundNoSpecialAsync();
            await asyncUseCases.IoBoundConfigureAwaitFalseAsync();
            await asyncUseCases.CpuBoundTasksAsync();
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
    }
}
