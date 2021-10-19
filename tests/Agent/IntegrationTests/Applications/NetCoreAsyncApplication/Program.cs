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
using ApplicationLifecycle;

namespace NetCoreAsyncApplication
{
    class Program
    {
        private static string _port;

        static void Main(string[] args)
        {
            _port = AppLifecycleManager.GetPortFromArgs(args);

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            var doWorkTask = Task.Run(async () => await DoWorkAsync(), cancellationTokenSource.Token);
            doWorkTask.Wait(cancellationTokenSource.Token);

            AppLifecycleManager.CreatePidFile();

            AppLifecycleManager.WaitForTestCompletion(_port);
        }

        private static async Task DoWorkAsync()
        {
            var asyncUseCases = new AsyncUseCases();

            await asyncUseCases.IoBoundNoSpecialAsync();
            await asyncUseCases.IoBoundConfigureAwaitFalseAsync();
            await asyncUseCases.CpuBoundTasksAsync();
        }

    }
}
