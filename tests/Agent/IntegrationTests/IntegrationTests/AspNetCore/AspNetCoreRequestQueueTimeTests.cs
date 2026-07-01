// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AspNetCore;

public class AspNetCoreRequestQueueTimeTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
{
    private readonly RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture _fixture;

    public AspNetCoreRequestQueueTimeTests(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
            },
            exerciseApplication: () =>
            {
                // Past timestamp: a few seconds before now, so queue time is positive.
                var pastMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds - 2000;
                _fixture.GetWithCustomHeaders(new Dictionary<string, string>
                {
                    { "X-Request-Start", $"t={pastMs}" }
                });

                // Future timestamp: clock skew case -- queue time must NOT be recorded.
                var futureMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds + 60000;
                _fixture.GetWithCustomHeaders(new Dictionary<string, string>
                {
                    { "X-Request-Start", $"t={futureMs}" }
                });

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                // Transaction events harvest on their own cycle (server-overridden fast in CI, else 60s);
                // wait for the analytic_event_data harvest so GetTransactionEvents() is not timing-dependent.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void QueueTimeMetricPresentForPastHeader()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = "WebFrontend/QueueTime", CallCountAllHarvests = 1 }
        };

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics)
        );

        // queueDuration intrinsic must be present on the transaction event for the request with the past header.
        // Use GetTransactionEvents() since two transactions are exercised and we want either web tx event.
        var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();

        NrAssert.Multiple(
            () => Assert.True(
                transactionEvents.Any(e => e.IntrinsicAttributes.ContainsKey("queueDuration")),
                "Expected at least one transaction event to contain the queueDuration intrinsic attribute.")
        );
    }

    [Fact]
    public void QueueTimeMetricExactlyOnceFromPastHeaderOnly()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        // The past-header request contributes exactly 1 call.
        // The future-dated request (clock skew) is silently dropped by the parser so it adds no call.
        // Aggregate all harvests: total call count across all WebFrontend/QueueTime entries should be 1.
        var queueTimeMetrics = metrics.Where(m => m.MetricSpec.Name == "WebFrontend/QueueTime").ToList();
        var totalCalls = queueTimeMetrics.Aggregate(0UL, (acc, m) => acc + m.Values.CallCount);
        Assert.True(
            totalCalls == 1,
            $"Expected exactly 1 WebFrontend/QueueTime call (from the past-header request only); got {totalCalls}.");
    }
}
