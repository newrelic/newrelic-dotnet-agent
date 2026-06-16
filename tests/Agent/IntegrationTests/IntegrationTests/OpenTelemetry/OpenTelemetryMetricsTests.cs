// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.IntegrationTests.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OpenTelemetry;

public abstract class OpenTelemetryMetricsTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : OtlpMetricsWithCollectorFixtureBase
{
    protected readonly TFixture _fixture;
    private IEnumerable<MetricsSummaryDto> _otlpSummaries;

    protected OpenTelemetryMetricsTestsBase(TFixture fixture, ITestOutputHelper outputHelper) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = outputHelper;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                // Set both interval and timeout to pass validation (interval must be > timeout)
                configModifier.SetOpenTelemetryMetricsExportInterval(5000); // 5 seconds
                configModifier.SetOpenTelemetryMetricsExportTimeout(4000); // 4 seconds
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));

                // Wait for actual OTLP metrics export (every 5s) rather than the analytics event
                // harvest (every 60s), which can race with the WaitForLogLine timeout on slow CI.
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.OtlpMetricsExportedLogLineRegex, TimeSpan.FromMinutes(1));

                _otlpSummaries = _fixture.GetCollectedOTLPMetrics();
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void OtlpMetrics_are_collected_with_expected_names_counts_and_histogram_aggregation()
    {
        Assert.NotNull(_otlpSummaries);
        Assert.NotEmpty(_otlpSummaries);

        // Aggregate metrics from summaries
        var metricEntries = new List<MetricSummary>();
        foreach (var s in _otlpSummaries)
        {
            foreach (var r in s.Resources)
            {
                foreach (var scope in r.Scopes)
                {
                    metricEntries.AddRange(scope.Metrics);
                }
            }
        }

        Assert.NotEmpty(metricEntries);

        // Aggregate to unique metric name and total count for that name
        var aggregatedTotals = metricEntries
            .GroupBy(m => m.Name)
            .Select(g => new { Name = g.Key, TotalCount = g.Sum(m => m.DataPointCount) })
            .OrderBy(x => x.Name)
            .ToList();

        // serialize aggregated metric entries for easier debugging on failure
        _fixture.TestLogger.WriteLine("Aggregated OTLP Metrics Totals:");
        foreach (var metric in aggregatedTotals)
        {
            _fixture.TestLogger.WriteLine($"Name: {metric.Name}, TotalCount: {metric.TotalCount}");
        }

        // Verify all expected metrics are present by name (ignore type differences across platforms)
        foreach (var expectedMetric in GetExpectedMetrics())
        {
            var found = aggregatedTotals.Any(m => m.Name == expectedMetric);
            Assert.True(found, $"Expected metric '{expectedMetric}' not found. Available metrics: {string.Join(", ", aggregatedTotals.Select(m => m.Name))}");
        }

        // The agent configures base-2 exponential bucket histograms (via an AddView registration on
        // the MeterProvider), so no histogram instrument should be exported as the SDK-default
        // explicit-bucket "Histogram". If that configuration silently regressed, the explicit-bucket
        // type would show up here.
        var explicitBucketHistograms = metricEntries
            .Where(m => m.Type == "Histogram")
            .Select(m => m.Name)
            .Distinct()
            .ToList();
        Assert.True(explicitBucketHistograms.Count == 0,
            $"Expected no explicit-bucket Histogram metrics (base-2 exponential is configured), but found: {string.Join(", ", explicitBucketHistograms)}");

        // Where a histogram instrument is exported on this platform, confirm it is an ExponentialHistogram.
        var histogramMetricName = GetExpectedExponentialHistogramMetricName();
        if (histogramMetricName != null)
        {
            var match = metricEntries.FirstOrDefault(m => m.Name == histogramMetricName);
            Assert.True(match != null,
                $"Expected histogram metric '{histogramMetricName}' was not exported. Available: {string.Join(", ", metricEntries.Select(m => m.Name).Distinct())}");
            Assert.Equal("ExponentialHistogram", match.Type);
        }
    }

    protected virtual string[] GetExpectedMetrics()
    {
        return new[] { "requests_total", "payload_size_bytes", "active_requests", "queue_depth", "cpu_usage_percent", "active_connections" };
    }

    // The histogram instrument expected to be exported on this platform, or null if histogram
    // instruments are not bridged here (e.g. .NET Framework, which omits payload_size_bytes).
    protected virtual string GetExpectedExponentialHistogramMetricName() => "payload_size_bytes";
}

// NET10 test targets DiagnosticSource v10.x
public class OpenTelemetryMetricsTestsCoreLatest : OpenTelemetryMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreLatest>
{
    public OpenTelemetryMetricsTestsCoreLatest(OtlpMetricsWithCollectorFixtureCoreLatest fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
}

// Net8 test targets DiagnosticSource v8.x
public class OpenTelemetryMetricsTestsCoreNet8 : OpenTelemetryMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreNet8>
{
    public OpenTelemetryMetricsTestsCoreNet8(OtlpMetricsWithCollectorFixtureCoreNet8 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
}

// Net472 test targets DiagnosticSource v8.x
public class OpenTelemetryMetricsTestsFw472 : OpenTelemetryMetricsTestsBase<OtlpMetricsWithCollectorFixtureFW472>
{
    public OpenTelemetryMetricsTestsFw472(OtlpMetricsWithCollectorFixtureFW472 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
    protected override string[] GetExpectedMetrics() => new[] { "active_requests", "queue_depth", "cpu_usage_percent", "active_connections" };
    protected override string GetExpectedExponentialHistogramMetricName() => null;
}

// Net481 test targets DiagnosticSource v9.x
public class OpenTelemetryMetricsTestsFw481 : OpenTelemetryMetricsTestsBase<OtlpMetricsWithCollectorFixtureFW481>
{
    public OpenTelemetryMetricsTestsFw481(OtlpMetricsWithCollectorFixtureFW481 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
    protected override string[] GetExpectedMetrics() => new[] { "active_requests", "queue_depth", "cpu_usage_percent", "active_connections" };
    protected override string GetExpectedExponentialHistogramMetricName() => null;
}
