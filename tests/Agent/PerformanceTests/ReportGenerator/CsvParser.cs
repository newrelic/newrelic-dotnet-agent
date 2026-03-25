// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ReportGenerator;

public static class CsvParser
{
    // locust_stats.csv column indices (0-based):
    //  0=Type, 1=Name, 2=Request Count, 3=Failure Count, 4=Median Response Time,
    //  5=Average Response Time, 6=Min Response Time, 7=Max Response Time,
    //  8=Average Content Size, 9=Requests/s, 10=Failures/s,
    //  11=50%, 12=66%, 13=75%, 14=80%, 15=90%, 16=95%, 17=98%, 18=99%, ...
    private const int ColRequestsPerSec = 9;
    private const int ColP50 = 11;
    private const int ColP95 = 16;
    private const int ColP99 = 18;

    public static (double RequestsPerSec, double P50Ms, double P95Ms, double P99Ms) ParseLocustStats(string csvPath)
    {
        if (!File.Exists(csvPath))
            return (0, 0, 0, 0);

        foreach (var line in File.ReadLines(csvPath))
        {
            var cols = line.Split(',');
            if (cols.Length <= ColP99)
                continue;

            // Name column is index 1; look for the "Aggregated" row
            if (!cols[1].Trim().Equals("Aggregated", StringComparison.OrdinalIgnoreCase))
                continue;

            double.TryParse(cols[ColRequestsPerSec].Trim(), out var rps);
            double.TryParse(cols[ColP50].Trim(), out var p50);
            double.TryParse(cols[ColP95].Trim(), out var p95);
            double.TryParse(cols[ColP99].Trim(), out var p99);

            return (rps, p50, p95, p99);
        }

        return (0, 0, 0, 0);
    }

    // docker-stats.csv columns: timestamp,cpu_pct,mem_pct,mem_usage,net_io,block_io,pids
    // cpu_pct: e.g. "12.34%"
    // mem_usage: e.g. "123MiB / 456MiB"  — parse the left side
    public static (double AvgCpuPct, double MaxCpuPct, double AvgMemMb) ParseDockerStats(string csvPath)
    {
        if (!File.Exists(csvPath))
            return (0, 0, 0);

        var cpuValues = new List<double>();
        var memValues = new List<double>();
        var isHeader = true;

        foreach (var line in File.ReadLines(csvPath))
        {
            if (isHeader)
            {
                isHeader = false;
                continue;
            }

            var cols = line.Split(',');
            if (cols.Length < 4)
                continue;

            var cpuStr = cols[1].Trim().TrimEnd('%');
            if (double.TryParse(cpuStr, out var cpu))
                cpuValues.Add(cpu);

            var memPart = cols[3].Split('/')[0].Trim(); // e.g. "123MiB"
            var memMb = ParseMemoryMb(memPart);
            if (memMb > 0)
                memValues.Add(memMb);
        }

        if (cpuValues.Count == 0)
            return (0, 0, 0);

        return (
            cpuValues.Average(),
            cpuValues.Max(),
            memValues.Count > 0 ? memValues.Average() : 0);
    }

    private static double ParseMemoryMb(string raw)
    {
        // Handles: "123MiB", "1.2GiB", "456kB", "789MB"
        raw = raw.Trim();
        if (raw.EndsWith("GiB", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(raw[..^3], out var v))
                return v * 1024;
        }
        else if (raw.EndsWith("MiB", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(raw[..^3], out var v))
                return v;
        }
        else if (raw.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(raw[..^2], out var v))
                return v;
        }
        else if (raw.EndsWith("kB", StringComparison.OrdinalIgnoreCase) ||
                 raw.EndsWith("KiB", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(raw[..^2], out var v))
                return v / 1024;
        }

        return 0;
    }
}
