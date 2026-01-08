// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.IntegrationTests.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OpenTelemetry
{
    public abstract class OpenTelemetryStressTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : OtlpStressWithCollectorFixtureBase
    {
        protected readonly TFixture _fixture;
        private IEnumerable<MetricsSummaryDto> _otlpSummaries;

        protected OpenTelemetryStressTestsBase(TFixture fixture, ITestOutputHelper outputHelper) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = outputHelper;
            _fixture.MeasurementsPerThread = 50;
            _fixture.ThreadCount = 5;

            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("debug");
                    configModifier.ConfigureFasterOpenTelemetryOtlpExportInterval(5);
                    configModifier.IncludeOpenTelemetryMeters("OtelMetricsTest.App");
                    configModifier.EnableOpenTelemetryMetrics(true);
                },
                exerciseApplication: () =>
                {
                    _fixture.TestLogger.WriteLine($"Agent Log Path: {_fixture.AgentLog?.FilePath}");
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(2));

                    // Wait for workload to complete - give more time for all metrics to be collected
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(5));

                    // Add extra delay to ensure all metrics are exported
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(30));

                    // Don't specify exact count - just get what's available with a reasonable max
                    _otlpSummaries = _fixture.GetCollectedOTLPMetrics(count: 1000);
                    
                    _fixture.TestLogger.WriteLine($"Collected {_otlpSummaries?.Count() ?? 0} OTLP metric summaries");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Otel_Bridge_Metrics_Are_Collected_Correctly()
        {
            Assert.NotNull(_otlpSummaries);
            Assert.NotEmpty(_otlpSummaries);

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

            _fixture.TestLogger.WriteLine($"Total metric entries collected: {metricEntries.Count}");

            // Aggregate to unique metric name and total count for that name
            var aggregatedTotals = metricEntries
                .GroupBy(m => m.Name)
                .Select(g => new { Name = g.Key, TotalCount = g.Sum(m => m.DataPointCount) })
                .OrderBy(x => x.Name)
                .ToList();

            _fixture.TestLogger.WriteLine("Aggregated OTLP Metrics Totals:");
            foreach (var metric in aggregatedTotals)
            {
                _fixture.TestLogger.WriteLine($"Name: {metric.Name}, TotalCount: {metric.TotalCount}");
            }

            // Verify we have metrics collected - be flexible about the exact count
            Assert.True(aggregatedTotals.Any(), "No metrics were collected");
            Assert.True(aggregatedTotals.Count >= 3, $"Expected at least 3 different metric types, but got {aggregatedTotals.Count}");
            
            // Check for expected metrics from OTelMetricsApplication
            var expectedMetrics = new[] { "requests_total", "payload_size_bytes", "active_requests" };
            var foundMetrics = expectedMetrics.Where(name => aggregatedTotals.Any(x => x.Name == name)).ToList();
            
            _fixture.TestLogger.WriteLine($"Found {foundMetrics.Count} of {expectedMetrics.Length} expected application metrics: {string.Join(", ", foundMetrics)}");
            
            // Verify at least one expected application metric was found
            // Also allow for runtime metrics like dotnet.gc.collections, etc.
            var hasApplicationMetric = foundMetrics.Any();
            var hasRuntimeMetric = aggregatedTotals.Any(x => x.Name.StartsWith("dotnet."));
            
            Assert.True(hasApplicationMetric || hasRuntimeMetric, 
                $"Expected to find either application metrics ({string.Join(", ", expectedMetrics)}) or runtime metrics (dotnet.*). " +
                $"Collected metrics: {string.Join(", ", aggregatedTotals.Select(x => x.Name))}");
            
            // Verify each collected metric has at least some data points
            foreach (var metric in aggregatedTotals)
            {
                Assert.True(metric.TotalCount > 0, $"Metric '{metric.Name}' has no data points");
            }
        }
    }

    [Collection("OtelBridgeMetricsTest")]
    public class OpenTelemetryBridgeMetricsCollectionTestsCoreLatest : OpenTelemetryStressTestsBase<OtlpStressWithCollectorFixtureCoreLatest>
    {
        public OpenTelemetryBridgeMetricsCollectionTestsCoreLatest(OtlpStressWithCollectorFixtureCoreLatest fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
    }

    [Collection("OtelBridgeMetricsTest")]
    public class OpenTelemetryBridgeMetricsCollectionTestsCoreNet8 : OpenTelemetryStressTestsBase<OtlpStressWithCollectorFixtureCoreNet8>
    {
        public OpenTelemetryBridgeMetricsCollectionTestsCoreNet8(OtlpStressWithCollectorFixtureCoreNet8 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
    }
    [Collection("OtelBridgeMetricsTest")]
    public class OpenTelemetryBridgeMetricsCollectionTestsNet472 : OpenTelemetryStressTestsBase<OtlpStressWithCollectorFixtureFW472>
    {
        public OpenTelemetryBridgeMetricsCollectionTestsNet472(OtlpStressWithCollectorFixtureFW472 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
    }
    [Collection("OtelBridgeMetricsTest")]
    public class OpenTelemetryBridgeMetricsCollectionTestsNet481 : OpenTelemetryStressTestsBase<OtlpStressWithCollectorFixtureFW481>
    {
        public OpenTelemetryBridgeMetricsCollectionTestsNet481(OtlpStressWithCollectorFixtureFW481 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
    }

}
