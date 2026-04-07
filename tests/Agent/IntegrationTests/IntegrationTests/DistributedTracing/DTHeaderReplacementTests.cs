// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing;

/// <summary>
/// Verifies that when outbound HTTP requests already have DT headers (traceparent, tracestate, newrelic),
/// the agent replaces them rather than duplicating or erroring.
/// Note: HttpClient header replacement is covered by unit tests (SendAsync uses Remove+Add which correctly replaces)
/// and by the existing HttpClientW3CTests for regression. The Owin self-hosted fixture has infrastructure
/// issues that prevent a standalone existing-headers test.
/// </summary>
public class HttpWebRequestDTHeaderReplacementTest : NewRelicIntegrationTest<FrameworkTracingChainFixture>
{
    private readonly FrameworkTracingChainFixture _fixture;

    public HttpWebRequestDTHeaderReplacementTest(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetOrDeleteSpanEventsEnabled(true);
                configModifier.SetOrDeleteDistributedTraceEnabled(true);
                configModifier.SetLogLevel("debug");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(10);

                var environmentVariables = new Dictionary<string, string>();

                _fixture.ReceiverApplication = _fixture.SetupReceiverApplication(isDistributedTracing: true, isWebApplication: true);
                _fixture.ReceiverApplication.Start(string.Empty, environmentVariables, captureStandardOutput: true);
            },
            exerciseApplication: () =>
            {
                _fixture.ExecuteTraceRequestChainHttpWebRequestWithExistingHeaders();

                _fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void AgentReplacesExistingDTHeaders()
    {
        var receiverMetrics = _fixture.ReceiverAppAgentLog.GetMetrics().ToArray();

        var receiverExpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"Supportability/TraceContext/Accept/Success", callCount = 1 },
        };

        Assertions.MetricsExist(receiverExpectedMetrics, receiverMetrics);

        var receiverTxEvent = _fixture.ReceiverAppAgentLog.GetTransactionEvents().FirstOrDefault();
        Assert.NotNull(receiverTxEvent);
        Assert.True(receiverTxEvent.IntrinsicAttributes.ContainsKey("parentId"), "Receiver should have parentId from replaced DT headers");
    }
}

public class RestSharpDTHeaderReplacementTest : NewRelicIntegrationTest<FrameworkTracingChainFixture>
{
    private readonly FrameworkTracingChainFixture _fixture;

    public RestSharpDTHeaderReplacementTest(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetOrDeleteSpanEventsEnabled(true);
                configModifier.SetOrDeleteDistributedTraceEnabled(true);
                configModifier.SetLogLevel("debug");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(10);

                var environmentVariables = new Dictionary<string, string>();

                _fixture.ReceiverApplication = _fixture.SetupReceiverApplication(isDistributedTracing: true, isWebApplication: true);
                _fixture.ReceiverApplication.Start(string.Empty, environmentVariables, captureStandardOutput: true);
            },
            exerciseApplication: () =>
            {
                _fixture.ExecuteTraceRequestChainRestSharpWithExistingHeaders();

                _fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void AgentReplacesExistingDTHeaders()
    {
        var receiverMetrics = _fixture.ReceiverAppAgentLog.GetMetrics().ToArray();

        var receiverExpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"Supportability/TraceContext/Accept/Success", callCount = 1 },
        };

        Assertions.MetricsExist(receiverExpectedMetrics, receiverMetrics);

        var receiverTxEvent = _fixture.ReceiverAppAgentLog.GetTransactionEvents().FirstOrDefault();
        Assert.NotNull(receiverTxEvent);
        Assert.True(receiverTxEvent.IntrinsicAttributes.ContainsKey("parentId"), "Receiver should have parentId from replaced DT headers");
    }
}
