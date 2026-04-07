// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.WCF;

/// <summary>
/// Verifies that when outbound WCF HTTP requests already have DT headers
/// (traceparent, tracestate, newrelic) in HttpRequestMessageProperty.Headers,
/// the agent replaces them rather than duplicating or erroring.
/// </summary>
public class WCFClient_Self_BasicHTTP_DTHeaderReplacement : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureFWLatest>
{
    private readonly ConsoleDynamicMethodFixtureFWLatest _fixture;

    public WCFClient_Self_BasicHTTP_DTHeaderReplacement(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.SetTimeout(TimeSpan.FromMinutes(3));

        var relativePath = "WCFService";

        // Start the self-hosted WCF service with BasicHttp binding
        _fixture.AddCommand($"WCFServiceSelfHosted StartService {WCFBindingType.BasicHttp} {_fixture.RemoteApplication.Port} {relativePath}");

        // Initialize client and call with existing DT headers
        _fixture.AddCommand($"WCFClient InitializeClient_SelfHosted {WCFBindingType.BasicHttp} {_fixture.RemoteApplication.Port} {relativePath}");
        _fixture.AddCommand("WCFClient GetDataWithExistingDTHeaders");

        // Stop service
        _fixture.AddCommand("WCFServiceSelfHosted StopService");

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                _fixture.RemoteApplication.NewRelicConfig.SetOrDeleteDistributedTraceEnabled(true);
                _fixture.RemoteApplication.NewRelicConfig.EnableSpanEvents(true);
                _fixture.RemoteApplication.NewRelicConfig.ForceTransactionTraces();
                _fixture.RemoteApplication.NewRelicConfig.ConfigureFasterMetricsHarvestCycle(10);
                _fixture.RemoteApplication.NewRelicConfig.ConfigureFasterSpanEventsHarvestCycle(10);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void AgentReplacesExistingDTHeaders()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = "Supportability/TraceContext/Create/Success", CallCountAllHarvests = 1 },
            new() { metricName = "Supportability/TraceContext/Accept/Success", CallCountAllHarvests = 1 },
        };

        Assertions.MetricsExist(expectedMetrics, metrics);

        // Verify the service-side transaction has DT attributes, proving the stale headers were replaced
        var serviceTxEvents = _fixture.AgentLog.GetTransactionEvents()
            .Where(e => e.IntrinsicAttributes.TryGetValue("name", out var name) &&
                        name.ToString().Contains("Wcf"))
            .ToList();

        // At least one service transaction should have parentId (from the replaced DT headers)
        Assert.True(serviceTxEvents.Any(e => e.IntrinsicAttributes.ContainsKey("parentId")),
            "At least one WCF service transaction should have parentId from replaced DT headers");
    }
}
