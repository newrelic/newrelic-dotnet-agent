// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;
using ReportGenerator;

Parser.Default.ParseArguments<Options>(args)
    .WithParsed(Run)
    .WithNotParsed(_ => Environment.Exit(1));

static void Run(Options opts)
{
    if (!Directory.Exists(opts.InputDir))
    {
        Console.Error.WriteLine($"ERROR: Input directory not found: {opts.InputDir}");
        Environment.Exit(1);
    }

    Directory.CreateDirectory(opts.OutputDir);

    var runDirs = Directory.GetDirectories(opts.InputDir)
        .Where(d => Path.GetFileName(d).StartsWith("perf-results-", StringComparison.OrdinalIgnoreCase))
        .OrderBy(d => d)
        .ToList();

    if (runDirs.Count == 0)
    {
        Console.Error.WriteLine($"ERROR: No perf-results-* subdirectories found in: {opts.InputDir}");
        Environment.Exit(1);
    }

    var runs = new List<RunMetrics>();

    foreach (var dir in runDirs)
    {
        var dirName = Path.GetFileName(dir);
        var label = dirName.StartsWith("perf-results-", StringComparison.OrdinalIgnoreCase)
            ? dirName["perf-results-".Length..]
            : dirName;

        var locustCsv = Path.Combine(dir, "locust_stats.csv");
        var statsCsv = Path.Combine(dir, "docker-stats.csv");

        var (rps, p50, p95, p99) = CsvParser.ParseLocustStats(locustCsv);
        var (avgCpu, maxCpu, avgMem) = CsvParser.ParseDockerStats(statsCsv);

        Console.WriteLine($"  {label}: {rps:F1} req/s  p50={p50:F0}ms  p95={p95:F0}ms  p99={p99:F0}ms  avgCPU={avgCpu:F1}%  avgMem={avgMem:F0}MB");

        runs.Add(new RunMetrics(label, rps, p50, p95, p99, avgCpu, maxCpu, avgMem));
    }

    Console.WriteLine($"Generating charts for {runs.Count} run(s)...");

    ChartBuilder.GenerateResponseTimeChart(runs, Path.Combine(opts.OutputDir, "response-time.png"));
    ChartBuilder.GenerateThroughputChart(runs, Path.Combine(opts.OutputDir, "throughput.png"));
    ChartBuilder.GenerateCpuChart(runs, Path.Combine(opts.OutputDir, "cpu-usage.png"));

    var table = MarkdownTableBuilder.Build(runs);

    var summary = new System.Text.StringBuilder();
    summary.AppendLine(table);
    summary.AppendLine();
    summary.AppendLine("### Charts");
    summary.AppendLine();
    summary.AppendLine("![Response Time](response-time.png)");
    summary.AppendLine("![Throughput](throughput.png)");
    summary.AppendLine("![CPU Usage](cpu-usage.png)");

    File.WriteAllText(Path.Combine(opts.OutputDir, "summary.md"), summary.ToString());

    Console.WriteLine("Report generation complete.");
}
