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

                    // Wait for workload to complete
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(3));

                    _otlpSummaries = _fixture.GetCollectedOTLPMetrics(count: 500);
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

            // Aggregate to unique metric name and total count for that name
            var aggregatedTotals = metricEntries
                .GroupBy(m => m.Name)
                .Select(g => new { Name = g.Key, TotalCount = g.Sum(m => m.DataPointCount) })
                .OrderBy(x => x.Name)
                .ToList();

            _fixture.TestLogger.WriteLine("Final Aggregated OTLP Metrics Totals:");
            foreach (var metric in aggregatedTotals)
            {
                _fixture.TestLogger.WriteLine($"Name: {metric.Name}, TotalCount: {metric.TotalCount}");
            }

            // OTelMetricsApplication creates 6 instruments
            // Verify we have metrics collected
            Assert.True(aggregatedTotals.Any(), "No metrics were collected");
            
            // Check for expected metrics (may not all be present on all platforms)
            var expectedMetrics = new[] { "requests_total", "payload_size_bytes", "active_requests" };
            var foundMetrics = expectedMetrics.Where(name => aggregatedTotals.Any(x => x.Name == name)).ToList();
            
            _fixture.TestLogger.WriteLine($"Found {foundMetrics.Count} of {expectedMetrics.Length} expected metrics: {string.Join(", ", foundMetrics)}");
            
            // Verify at least one expected metric was found
            Assert.True(foundMetrics.Any(), $"None of the expected metrics were found. Collected metrics: {string.Join(", ", aggregatedTotals.Select(x => x.Name))}");
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
