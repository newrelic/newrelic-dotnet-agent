// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace ReportGenerator;

public static class MarkdownTableBuilder
{
    public static string Build(IList<RunMetrics> runs)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Performance Comparison Report");
        sb.AppendLine();
        sb.AppendLine("| Run | Req/s | p50 (ms) | p95 (ms) | p99 (ms) | Avg CPU % | Max CPU % | Avg Mem (MB) |");
        sb.AppendLine("|-----|------:|---------:|---------:|---------:|----------:|----------:|-------------:|");

        foreach (var run in runs)
        {
            sb.AppendLine(
                $"| {run.Label} " +
                $"| {run.RequestsPerSec:F1} " +
                $"| {run.P50Ms:F0} " +
                $"| {run.P95Ms:F0} " +
                $"| {run.P99Ms:F0} " +
                $"| {run.AvgCpuPct:F1} " +
                $"| {run.MaxCpuPct:F1} " +
                $"| {run.AvgMemMb:F0} |");
        }

        return sb.ToString();
    }
}
