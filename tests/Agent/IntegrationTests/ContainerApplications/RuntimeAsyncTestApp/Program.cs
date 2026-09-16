// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RuntimeAsyncTestApp;

public class Program
{
    // Deliberately not async. runtime-async=on applies to the whole assembly, so an async Main
    // would itself be compiled runtime-async and would be the very first thing the runtime has to
    // execute -- a needless risk for a method that only has to block until startup work is done.
    public static void Main(string[] args)
    {
        // Fail loudly and early if this build is not actually runtime-async. Runs before the web
        // host starts so a mis-built application can never look like a passing test.
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

        // The use cases run at startup rather than from a request handler, and that is load
        // bearing. Their [Transaction] attributes are meant to create OtherTransactions; called
        // inside a web request they would instead become segments of that request's
        // WebTransaction, which changes every metric name and the nesting the test asserts on.
        // Running them here mirrors the host-run console application exactly.
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        var doWorkTask = Task.Run(async () => await DoWorkAsync(), cancellationTokenSource.Token);
        doWorkTask.Wait(cancellationTokenSource.Token);

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();

        // listen to any ip on port 80 for http
        var ipEndPointHttp = new IPEndPoint(IPAddress.Any, 80);
        builder.WebHost.UseUrls($"http://{ipEndPointHttp}");

        var app = builder.Build();
        app.UseAuthorization();
        app.MapControllers();

        // The web host exists only so the harness has something to connect to and so the container
        // stays up long enough for a harvest; the telemetry under test is already recorded by now.
        app.StartAsync().GetAwaiter().GetResult();

        CreatePidFile();

        app.WaitForShutdownAsync().GetAwaiter().GetResult();
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
        // is sampled and produces its span no matter how many ran before it.
        useCases.SynchronousControl();
    }

    public static void CreatePidFile()
    {
        var pidFileNameAndPath = Path.Combine(Environment.GetEnvironmentVariable("NEW_RELIC_LOG_DIRECTORY"), "containerizedapp.pid");
        var pid = Environment.ProcessId;
        using var file = File.CreateText(pidFileNameAndPath);
        file.WriteLine(pid);
    }
}
