// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using ApplicationLifecycle;
using NewRelic.Api.Agent;

class Program
{
    static Program()
    {
        // Force-load DiagnosticSource before agent initializes
        _ = typeof(Meter).Assembly;
    }
    
    static async Task Main(string[] args)
    {
        var port = AppLifecycleManager.GetPortFromArgs(args);
        var measurementsPerThread = GetArgValue(args, "--measurements", 1000);
        var threadCount = GetArgValue(args, "--threads", 10);

        var eventWaitHandleName = "app_server_wait_for_all_request_done_" + port;
        using var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventWaitHandleName);

        AppLifecycleManager.CreatePidFile();

        var meters = new List<Meter>();
        var counters = new List<Counter<long>>();
        var histograms = new List<Histogram<long>>();
        var upDownCounters = new List<UpDownCounter<long>>();

        for (int i = 1; i <= 5; i++)
        {
            var meter = new Meter($"OtelStress.Meter.{i}", "1.0.0");
            meters.Add(meter);
            counters.Add(meter.CreateCounter<long>($"counter_{i}"));
            histograms.Add(meter.CreateHistogram<long>($"histogram_{i}"));
            upDownCounters.Add(meter.CreateUpDownCounter<long>($"updown_{i}"));
        }

        Console.WriteLine($"Starting OtelStress workload: {threadCount} threads, {measurementsPerThread} measurements/thread (Total: {threadCount * measurementsPerThread})");

        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(() => RecordMeasurements(threadId, measurementsPerThread, counters, histograms, upDownCounters)));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        NewRelic.Api.Agent.NewRelic.RecordCustomEvent("OtelStressComplete", new Dictionary<string, object>
        {
            { "TimeMs", sw.ElapsedMilliseconds },
            { "RecPerSec", (threadCount * measurementsPerThread) / sw.Elapsed.TotalSeconds }
        });

        Console.WriteLine($"OtelStress complete. Time: {sw.ElapsedMilliseconds}ms. Throughput: {(threadCount * measurementsPerThread) / sw.Elapsed.TotalSeconds:F2} rec/sec");

        // Wait for a bit to ensure export can happen
        await Task.Delay(5000);

        Console.WriteLine("Waiting for signal to terminate.");
        eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));

        foreach (var meter in meters)
        {
            meter.Dispose();
        }
    }

    [Transaction]
    private static void RecordMeasurements(int threadId, int count, List<Counter<long>> counters, List<Histogram<long>> histograms, List<UpDownCounter<long>> upDownCounters)
    {
        var rand = new Random();
        for (int i = 0; i < count; i++)
        {
            var meterIdx = rand.Next(0, 5);
            var tags = new TagList { { "thread", threadId }, { "iteration", i % 10 } };
            
            counters[meterIdx].Add(1, tags);
            histograms[meterIdx].Record(rand.Next(1, 100), tags);
            upDownCounters[meterIdx].Add(rand.Next(-1, 2), tags);

            if (i % 100 == 0)
            {
                // Yield occasionally
                Thread.Sleep(rand.Next(0, 2));
            }
        }
    }

    private static int GetArgValue(string[] args, string name, int defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return int.Parse(args[i + 1]);
            }
        }
        return defaultValue;
    }
}
