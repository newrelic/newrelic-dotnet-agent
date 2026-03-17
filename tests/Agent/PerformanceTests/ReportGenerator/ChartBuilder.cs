// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ScottPlot;

namespace ReportGenerator;

public static class ChartBuilder
{
    private const int ChartWidth = 900;
    private const int ChartHeight = 500;
    private const double BarWidth = 0.25;
    private const double GroupSpacing = 1.0;

    public static void GenerateResponseTimeChart(IList<RunMetrics> runs, string outputPath)
    {
        var plot = new Plot();

        var p50Bars = new List<Bar>();
        var p95Bars = new List<Bar>();
        var p99Bars = new List<Bar>();

        for (var i = 0; i < runs.Count; i++)
        {
            var center = i * GroupSpacing;
            p50Bars.Add(new Bar { Position = center - BarWidth, Value = runs[i].P50Ms, Size = BarWidth });
            p95Bars.Add(new Bar { Position = center, Value = runs[i].P95Ms, Size = BarWidth });
            p99Bars.Add(new Bar { Position = center + BarWidth, Value = runs[i].P99Ms, Size = BarWidth });
        }

        var bp50 = plot.Add.Bars(p50Bars);
        bp50.LegendText = "p50";

        var bp95 = plot.Add.Bars(p95Bars);
        bp95.LegendText = "p95";

        var bp99 = plot.Add.Bars(p99Bars);
        bp99.LegendText = "p99";

        SetGroupAxisLabels(plot, runs);
        plot.Title("Response Time by Percentile (ms)");
        plot.YLabel("Milliseconds");
        plot.ShowLegend();
        plot.SavePng(outputPath, ChartWidth, ChartHeight);
    }

    public static void GenerateThroughputChart(IList<RunMetrics> runs, string outputPath)
    {
        var plot = new Plot();

        var bars = runs.Select((r, i) => new Bar { Position = i * GroupSpacing, Value = r.RequestsPerSec, Size = 0.6 }).ToList();

        plot.Add.Bars(bars);

        SetGroupAxisLabels(plot, runs);
        plot.Title("Throughput (Requests/s)");
        plot.YLabel("Requests per Second");
        plot.SavePng(outputPath, ChartWidth, ChartHeight);
    }

    public static void GenerateCpuChart(IList<RunMetrics> runs, string outputPath)
    {
        var plot = new Plot();

        var avgBars = new List<Bar>();
        var maxBars = new List<Bar>();

        for (var i = 0; i < runs.Count; i++)
        {
            var center = i * GroupSpacing;
            avgBars.Add(new Bar { Position = center - BarWidth / 2, Value = runs[i].AvgCpuPct, Size = BarWidth });
            maxBars.Add(new Bar { Position = center + BarWidth / 2, Value = runs[i].MaxCpuPct, Size = BarWidth });
        }

        var bpAvg = plot.Add.Bars(avgBars);
        bpAvg.LegendText = "Avg CPU %";

        var bpMax = plot.Add.Bars(maxBars);
        bpMax.LegendText = "Max CPU %";

        SetGroupAxisLabels(plot, runs);
        plot.Title("CPU Usage (%)");
        plot.YLabel("CPU %");
        plot.ShowLegend();
        plot.SavePng(outputPath, ChartWidth, ChartHeight);
    }

    private static void SetGroupAxisLabels(Plot plot, IList<RunMetrics> runs)
    {
        var positions = Enumerable.Range(0, runs.Count).Select(i => (double)i * GroupSpacing).ToArray();
        var labels = runs.Select(r => r.Label).ToArray();
        plot.Axes.Bottom.SetTicks(positions, labels);
    }
}
