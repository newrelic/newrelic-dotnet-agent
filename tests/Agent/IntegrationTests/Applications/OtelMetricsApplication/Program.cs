// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Exporter;
using System.Diagnostics;
using System.Diagnostics.Metrics;

class Program
{
    static async Task Main(string[] args)
    {
        // Indicate target framework
#if NETFRAMEWORK
        Console.WriteLine("Running target: .NET Framework 4.7.2");
#else
        Console.WriteLine("Running target: .NET 10.0");
#endif

        using var meter = new Meter("OtelMetricsTest.App", "1.0.0");

        // Instruments
        var requestCounter = meter.CreateCounter<long>("requests_total", description: "Total number of requests");
        var payloadSizeHistogram = meter.CreateHistogram<long>("payload_size_bytes", unit: "bytes", description: "Histogram of payload sizes in bytes");
        var activeRequestsUpDown = meter.CreateUpDownCounter<long>("active_requests", description: "Active requests in progress");
        meter.CreateObservableGauge("queue_depth", () => new Measurement<int>(CurrentQueueDepth), description: "Current queue depth");
        meter.CreateObservableCounter("cpu_usage_percent", () => new Measurement<double>(GetCpuUsagePercent()), unit: "%", description: "CPU usage percentage");
        meter.CreateObservableUpDownCounter("active_connections", () => new Measurement<int>(CurrentActiveConnections), description: "Active connections observed");

        // Build MeterProvider with Console and OTLP HTTP exporter
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddConsoleExporter()
            //.AddOtlpExporter(options =>
            //{
            //    options.Protocol = OtlpExportProtocol.HttpProtobuf;
            //    options.Endpoint = new Uri("http://localhost:24279/v1/metrics");
            //})
            .Build();

        var start = DateTime.UtcNow;
        var end = start.AddSeconds(30);
        var rand = new Random();

        while (DateTime.UtcNow < end)
        {
            // Simulate a request coming in
            var routeTags = new TagList { new("route", "/api/test") };
            requestCounter.Add(1, routeTags);
            activeRequestsUpDown.Add(1);

            // Simulate payload sizes
            var payload = rand.Next(100, 50_000);
            payloadSizeHistogram.Record(payload, routeTags);

            // Update observables via backing fields
            CurrentQueueDepth = rand.Next(0, 1000);
            // Simulate connection changes for observable updown counter
            var delta = rand.Next(-3, 4); // -3..+3
            CurrentActiveConnections = Math.Max(0, CurrentActiveConnections + delta);

            // Simulate long-running work then mark completion
            await Task.Delay(rand.Next(50, 200));
            activeRequestsUpDown.Add(-1);

            // Occasionally simulate another route
            if (rand.NextDouble() < 0.2)
            {
                var otherRouteTags = new TagList { new("route", "/api/other") };
                requestCounter.Add(1, otherRouteTags);
                payloadSizeHistogram.Record(rand.Next(10, 10_000), otherRouteTags);
            }
        }

        // Small final delay to allow exporter to flush on process end
        await Task.Delay(250);
        Console.WriteLine("OtelMetricsTest complete.");
    }

    // Backing data for observable instruments
    private static int CurrentQueueDepth;
    private static int CurrentActiveConnections;

    private static double GetCpuUsagePercent()
    {
        // Simple simulated CPU usage; replace with real measurement if desired
        return 5 + (DateTime.UtcNow.Millisecond % 95);
    }
}
