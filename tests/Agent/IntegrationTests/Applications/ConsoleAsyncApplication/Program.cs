// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleAsyncApplication
{
    class Program
    {
        private const string DefaultPort = "5001";

        private static string _applicationName;

        static void Main(string[] args)
        {
            _applicationName =
                Path.GetFileNameWithoutExtension(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath) + ".exe";

            Console.WriteLine($"[{_applicationName}] Invoked with args: {string.Join(" ", args)}");

            var port = GetPortFromArgs(args) ?? DefaultPort;

            Console.WriteLine($"[{_applicationName}] Parsed port: {port}");

            var cancellationTokenSource = new CancellationTokenSource();

            var eventWaitHandleName = "app_server_wait_for_all_request_done_" + port;

            Console.WriteLine($"[{_applicationName}] Setting EventWaitHandle name to: {eventWaitHandleName}");

            Console.WriteLine("PID {0} Creating EventWaitHandle {1}", Process.GetCurrentProcess().Id, eventWaitHandleName);
            using (var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventWaitHandleName))
            {

                CreatePidFile();

                var doWorkTask = Task.Run(async () => await DoWorkAsyncAwait());
                doWorkTask.Wait();

                var doFireAndForgetTask = Task.Run(async () => await DoAsyncFireAndForgetWork());
                doFireAndForgetTask.Wait();

                DoWorkManualAsync();

                DoSyncFireAndForgetWork();

                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
            }
        }

        private static async Task DoWorkAsyncAwait()
        {
            var asyncAwaitUseCases = new AsyncAwaitUseCases();

            await asyncAwaitUseCases.IoBoundNoSpecialAsync();
            await asyncAwaitUseCases.IoBoundConfigureAwaitFalseAsync();
            await asyncAwaitUseCases.CpuBoundTasksAsync();
        }
        private static void DoWorkManualAsync()
        {
            var manualAsyncUseCases = new ManualAsyncUseCases();
            manualAsyncUseCases.TaskRunBlocked();
            manualAsyncUseCases.TaskFactoryStartNewBlocked();
            manualAsyncUseCases.NewThreadStartBlocked();
            manualAsyncUseCases.MultipleThreadSegmentParenting();
        }

        public static async Task DoAsyncFireAndForgetWork()
        {
            var waitHandleName = "DoAsyncFireAndForgetWork_" + Guid.NewGuid().ToString();
            Console.WriteLine("PID {0} Creating EventWaitHandle {1}", Process.GetCurrentProcess().Id, waitHandleName);
            using (var waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, waitHandleName))
            {
                const int waitSignalDelaySeconds = 30;

                var asyncFireAndForgetCases = new AsyncFireAndForgetUseCases();

                await asyncFireAndForgetCases.Async_AwaitedAsync();
                await asyncFireAndForgetCases.Async_FireAndForget(waitHandle);
                await asyncFireAndForgetCases.Async_Sync();

                var finished = waitHandle.WaitOne(TimeSpan.FromSeconds(waitSignalDelaySeconds));

                if (!finished)
                {
                    throw new Exception(
                        $"DoAsyncFireAndForgetWork did not receive expected singal '{waitHandleName}' within ${waitSignalDelaySeconds} seconds.");
                }
            }
        }

        public static void DoSyncFireAndForgetWork()
        {
            var waitHandleName = "DoSyncFireAndForgetWork_" + Guid.NewGuid().ToString();
            Console.WriteLine("PID {0} Creating EventWaitHandle {1}", Process.GetCurrentProcess().Id, waitHandleName);
            using (var waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, waitHandleName))
            {

                const int waitSignalDelaySeconds = 30;

                var syncFireAndForgetCases = new AsyncFireAndForgetUseCases();

                syncFireAndForgetCases.Sync_AwaitedAsync();
                syncFireAndForgetCases.Sync_FireAndForget(waitHandle);
                syncFireAndForgetCases.Sync_Sync();

                var finished = waitHandle.WaitOne(TimeSpan.FromSeconds(waitSignalDelaySeconds));

                if (!finished)
                {
                    throw new Exception(
                        $"DoSyncFireAndForget did not receive expected singal '{waitHandleName}' within ${waitSignalDelaySeconds} seconds.");
                }
            }
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
