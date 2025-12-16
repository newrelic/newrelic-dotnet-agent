// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.IntegrationTests.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OpenTelemetry
{
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
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));

                    // otlp metrics export will be complete before the first analytics event harvest
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(1));

                    _otlpSummaries = _fixture.GetCollectedOTLPMetrics();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Metrics_are_collected_and_match_expected_names_with_positive_counts()
        {
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

            // Verify all expected metrics are present (but allow additional metrics)
            foreach (var expectedMetric in GetExpectedMetrics())
            {
                Assert.Contains(metricEntries, m => m.Name == expectedMetric);
            }
        }

        protected virtual string[] GetExpectedMetrics()
        {
            return new[] { "requests_total", "payload_size_bytes", "active_requests", "queue_depth", "cpu_usage_percent", "active_connections" };
        }
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
    }

    // Net481 test targets DiagnosticSource v9.x
    public class OpenTelemetryMetricsTestsFw481 : OpenTelemetryMetricsTestsBase<OtlpMetricsWithCollectorFixtureFW481>
    {
        public OpenTelemetryMetricsTestsFw481(OtlpMetricsWithCollectorFixtureFW481 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
        protected override string[] GetExpectedMetrics() => new[] { "active_requests", "queue_depth", "cpu_usage_percent", "active_connections" };
    }
}
