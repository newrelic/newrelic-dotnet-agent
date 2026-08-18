// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation;

/// <summary>
/// Tests that WebFrontend/QueueTime is produced when X-Request-Start is sent to a classic ASP.NET/IIS app.
/// The UseHeaderBasedRequestQueueTimeForClassicAspNet appSettings key (default true) controls whether header-based or
/// IIS-internal (HttpWorkerRequest) queue time is used. Both paths call SetQueueTime, so the metric
/// and queueDuration intrinsic are present either way; the difference is the source of the measurement.
/// These tests run against the BasicMvcApplication under IIS -- skip locally if IIS is not configured.
/// </summary>
public class BasicMvcRequestQueueTimeDefaultTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
{
    private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

    public BasicMvcRequestQueueTimeDefaultTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
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
                configModifier.ForceTransactionTraces();
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                // UseHeaderBasedRequestQueueTimeForClassicAspNet defaults to true -- no override needed here.
            },
            exerciseApplication: () =>
            {
                // Past timestamp in ms: header-based path should pick this up.
                var pastMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds - 2000;
                var headers = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("X-Request-Start", $"t={pastMs}")
                };
                _fixture.GetWithHeaders(headers);

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                // Transaction events harvest on their own cycle (server-overridden fast in CI, else 60s);
                // wait for the analytic_event_data harvest so the transaction event is available.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void QueueTimeMetricAndAttributePresentWithHeaderBasedDefault()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = "WebFrontend/QueueTime", CallCountAllHarvests = 1 }
        };

        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(RemoteServiceFixtures.BasicMvcApplicationTestFixture.ExpectedTransactionName);

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assert.NotNull(transactionEvent),
            () => Assert.True(
                transactionEvent.IntrinsicAttributes.ContainsKey("queueDuration"),
                "Expected queueDuration intrinsic attribute on the transaction event.")
        );
    }
}

/// <summary>
/// Tests that with UseHeaderBasedRequestQueueTimeForClassicAspNet=false, IIS internal queue time is still reported
/// (via HttpWorkerRequest), so WebFrontend/QueueTime and queueDuration are still present.
/// The X-Request-Start header is present but must be ignored for the measurement source.
/// </summary>
public class BasicMvcRequestQueueTimeSwitchOffTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
{
    private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

    public BasicMvcRequestQueueTimeSwitchOffTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
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
                configModifier.ForceTransactionTraces();
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                // Disable header-based path; IIS internal HttpWorkerRequest timing takes over.
                CommonUtils.SetConfigAppSetting(configPath, "UseHeaderBasedRequestQueueTimeForClassicAspNet", "false", "urn:newrelic-config");
            },
            exerciseApplication: () =>
            {
                var pastMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds - 2000;
                var headers = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("X-Request-Start", $"t={pastMs}")
                };
                _fixture.GetWithHeaders(headers);

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                // Transaction events harvest on their own cycle (server-overridden fast in CI, else 60s);
                // wait for the analytic_event_data harvest so the transaction event is available.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void QueueTimeMetricPresentViaIisInternalTimingWhenSwitchOff()
    {
        // With the switch off, the header is ignored but IIS HttpWorkerRequest provides queue time.
        // The metric and attribute are still expected to be present.
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = "WebFrontend/QueueTime", CallCountAllHarvests = 1 }
        };

        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(RemoteServiceFixtures.BasicMvcApplicationTestFixture.ExpectedTransactionName);

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assert.NotNull(transactionEvent),
            () => Assert.True(
                transactionEvent.IntrinsicAttributes.ContainsKey("queueDuration"),
                "Expected queueDuration intrinsic attribute on the transaction event even when UseHeaderBasedRequestQueueTimeForClassicAspNet=false.")
        );
    }
}
