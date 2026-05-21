// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using NewRelic.Api.Agent;

namespace NewRelic.SignalRPoc.Client;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--burst")
        {
            await RunBurstAsync(args);
        }
        else
        {
            await RunSingleClientAsync(args);
        }

        await WaitForAgentHarvestAsync();
    }

    // The .NET agent harvests transaction, error, span, and metric data on a
    // 60-second cycle. A short-lived console run finishes faster than that, so
    // without an explicit wait the data created during the run never leaves the
    // agent and never lands in NRDB. Wait one full cycle plus a small buffer
    // before letting the process exit. Override with NRPOC_HARVEST_WAIT_SECONDS=0
    // to skip the wait (e.g. for local dev iteration that doesn't need NRDB).
    private static async Task WaitForAgentHarvestAsync()
    {
        var seconds = 65;
        var fromEnv = Environment.GetEnvironmentVariable("NRPOC_HARVEST_WAIT_SECONDS");
        if (!string.IsNullOrEmpty(fromEnv) && int.TryParse(fromEnv, out var parsed) && parsed >= 0)
        {
            seconds = parsed;
        }

        if (seconds == 0)
        {
            return;
        }

        Console.WriteLine($"Waiting {seconds}s for agent harvest cycle to flush. Ctrl+C to skip.");
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
        }
        catch (TaskCanceledException)
        {
            // user cancelled — fine
        }
        Console.WriteLine("Harvest wait complete; exiting.");
    }

    private static async Task RunSingleClientAsync(string[] args)
    {
        var hubUrl = args.Length > 0 ? args[0] : "http://localhost:5050/chathub";
        var iterations = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;

        Console.WriteLine($"Connecting to {hubUrl} (iterations={iterations})");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        connection.On<string, string>("ReceiveMessage", (user, message) =>
            Console.WriteLine($"  <- ReceiveMessage: {user}: {message}"));

        await connection.StartAsync();
        Console.WriteLine($"Connected. ConnectionId={connection.ConnectionId}");

        for (var i = 0; i < iterations; i++)
        {
            Console.WriteLine($"--- Iteration {i + 1}/{iterations} ---");
            await DriveOneIterationAsync(connection, i);
        }

        await connection.StopAsync();
        await connection.DisposeAsync();
        Console.WriteLine("Disconnected. Done.");
    }

    // Each iteration is its own New Relic transaction so that the SignalR.Client
    // activities created inside InvokeAsync/StreamAsync land as segments under it
    // and the bridge can propagate distributed-tracing context to the server.
    [Transaction]
    private static async Task DriveOneIterationAsync(HubConnection connection, int iteration)
    {
        var echo = await connection.InvokeAsync<string>("SendMessage", "poc-client", $"hello #{iteration}");
        Console.WriteLine($"  -> SendMessage returned: {echo}");

        try
        {
            await connection.InvokeAsync("ThrowSomething", $"iter-{iteration}");
            Console.WriteLine("  !! Expected exception, none thrown");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> ThrowSomething (expected): {ex.GetType().Name}: {ex.Message}");
        }

        await foreach (var item in connection.StreamAsync<int>("Counter", 5, 100))
        {
            Console.WriteLine($"  -> Counter yielded: {item}");
        }
    }

    // --burst <hubUrl> <connections> <durationSec> [keystrokeMs] [searchTerm]
    //   Simulates an autocomplete workload: opens N concurrent HubConnections,
    //   each repeatedly typing the search term one character at a time (sending
    //   one Search invocation per "keystroke") for the configured duration.
    //   Reports throughput and per-call latency client-side so we can compare
    //   agent-attached vs. baseline runs.
    private static async Task RunBurstAsync(string[] args)
    {
        var hubUrl       = args.Length > 1 ? args[1] : "http://localhost:5050/chathub";
        var connections  = args.Length > 2 && int.TryParse(args[2], out var c) ? c : 10;
        var durationSec  = args.Length > 3 && int.TryParse(args[3], out var d) ? d : 30;
        var keystrokeMs  = args.Length > 4 && int.TryParse(args[4], out var k) ? k : 80;
        var searchTerm   = args.Length > 5 ? args[5] : "alpha";

        Console.WriteLine(
            $"BURST: hub={hubUrl} connections={connections} duration={durationSec}s " +
            $"keystrokeMs={keystrokeMs} searchTerm='{searchTerm}'");

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSec));

        var clientTasks = new Task<BurstResult>[connections];
        for (var i = 0; i < connections; i++)
        {
            var clientId = i;
            clientTasks[i] = Task.Run(() => DriveOneClientAsync(
                hubUrl, clientId, keystrokeMs, searchTerm, cts.Token));
        }

        var swTotal = Stopwatch.StartNew();
        var results = await Task.WhenAll(clientTasks);
        swTotal.Stop();

        var totalInvocations = results.Sum(r => r.Invocations);
        var totalErrors      = results.Sum(r => r.Errors);
        var allLatencies     = results.SelectMany(r => r.LatenciesMs).OrderBy(x => x).ToArray();

        double percentile(double p) =>
            allLatencies.Length == 0
                ? 0
                : allLatencies[(int)Math.Min(allLatencies.Length - 1, allLatencies.Length * p)];

        Console.WriteLine();
        Console.WriteLine("=== BURST RESULTS ===");
        Console.WriteLine($"  Wall time         : {swTotal.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"  Connections       : {connections}");
        Console.WriteLine($"  Total invocations : {totalInvocations}");
        Console.WriteLine($"  Errors            : {totalErrors}");
        Console.WriteLine($"  Throughput        : {totalInvocations / swTotal.Elapsed.TotalSeconds:F1} invocations/sec");
        Console.WriteLine($"  Latency p50 / p95 / p99 (ms): " +
                          $"{percentile(0.50):F2} / {percentile(0.95):F2} / {percentile(0.99):F2}");
        Console.WriteLine($"  Latency max (ms)  : {(allLatencies.Length == 0 ? 0 : allLatencies[^1]):F2}");
    }

    private static async Task<BurstResult> DriveOneClientAsync(
        string hubUrl, int clientId, int keystrokeMs, string searchTerm, CancellationToken ct)
    {
        var result = new BurstResult();

        HubConnection? connection = null;
        try
        {
            connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .Build();

            await connection.StartAsync(ct);

            var sw = new Stopwatch();
            while (!ct.IsCancellationRequested)
            {
                // Type the term one character at a time, then erase back to length 1.
                for (var len = 1; len <= searchTerm.Length && !ct.IsCancellationRequested; len++)
                {
                    await InvokeOneSearchAsync(connection, searchTerm[..len], sw, result, ct);
                    await SafeDelayAsync(keystrokeMs, ct);
                }
                for (var len = searchTerm.Length - 1; len >= 1 && !ct.IsCancellationRequested; len--)
                {
                    await InvokeOneSearchAsync(connection, searchTerm[..len], sw, result, ct);
                    await SafeDelayAsync(keystrokeMs, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected at end of run
        }
        catch (Exception ex)
        {
            Console.WriteLine($"client {clientId}: fatal {ex.GetType().Name}: {ex.Message}");
            result.Errors++;
        }
        finally
        {
            if (connection is not null)
            {
                try { await connection.StopAsync(CancellationToken.None); } catch { /* ignore */ }
                await connection.DisposeAsync();
            }
        }

        return result;
    }

    // One transaction per simulated keystroke. The bridge attaches the
    // SignalR.Client InvocationOut activity as a segment under this transaction
    // and propagates DT context onto the activity so the server-side hub
    // method invocation joins the same trace.
    [Transaction]
    private static async Task InvokeOneSearchAsync(
        HubConnection connection, string prefix, Stopwatch sw, BurstResult result, CancellationToken ct)
    {
        sw.Restart();
        try
        {
            _ = await connection.InvokeAsync<string[]>("Search", prefix, ct);
            sw.Stop();
            result.Invocations++;
            result.LatenciesMs.Add(sw.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            sw.Stop();
            result.Errors++;
        }
    }

    private static async Task SafeDelayAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) { /* expected */ }
    }
}

internal sealed class BurstResult
{
    public long Invocations;
    public long Errors;
    public List<double> LatenciesMs { get; } = new(capacity: 4096);
}
