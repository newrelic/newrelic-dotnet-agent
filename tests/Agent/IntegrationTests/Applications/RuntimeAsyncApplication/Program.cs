// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLifecycle;

namespace RuntimeAsyncApplication;

class Program
{
    private static string _port;

    static void Main(string[] args)
    {
        _port = AppLifecycleManager.GetPortFromArgs(args);

        // Fail loudly and early if this build is not actually runtime-async. Runs before the
        // pid file is created so a mis-built application can never look like a passing test.
        try
        {
            RuntimeAsyncUseCases.VerifyCompiledAsRuntimeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"RUNTIME-ASYNC PRECONDITION FAILED: {ex.Message}");
            Console.Out.WriteLine($"RUNTIME-ASYNC PRECONDITION FAILED: {ex.Message}");
            throw;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        var doWorkTask = Task.Run(async () => await DoWorkAsync(), cancellationTokenSource.Token);
        doWorkTask.Wait(cancellationTokenSource.Token);

        AppLifecycleManager.CreatePidFile();

        AppLifecycleManager.WaitForTestCompletion(_port);
    }

    private static async Task DoWorkAsync()
    {
        var useCases = new RuntimeAsyncUseCases();

        // Repeat so the nested-segment assertion is a count, not a single sighting. The failure
        // this guards against dropped roughly a quarter of nested segments rather than all of
        // them, so one invocation would pass even with the bug present.
        for (var i = 0; i < RuntimeAsyncUseCases.Iterations; i++)
        {
            await useCases.OuterAsync(i);
        }

        // Deliberately last. The test sets the root span sampler to alwaysOn, so this transaction
        // is sampled and produces its span no matter how many ran before it -- ordering it first to
        // win the adaptive sampler is exactly the hidden constraint that configuration removes. If
        // the thread.id positive control ever fails, check that setting rather than this position.
        useCases.SynchronousControl();
    }
}
