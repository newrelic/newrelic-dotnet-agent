// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.HybridHttpContextStorage;

public class HybridHttpContextStorageTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicWebFormsApplication>
{

    private readonly RemoteServiceFixtures.BasicWebFormsApplication _fixture;

    public HybridHttpContextStorageTests(RemoteServiceFixtures.BasicWebFormsApplication fixture,
        ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.EnableHybridHttpContextStorage(true)
                    .ForceTransactionTraces();

                //_fixture.SetAdditionalEnvironmentVariable("NEW_RELIC_HYBRID_HTTP_CONTEXT_STORAGE_ENABLED", "true");
            },
            exerciseApplication: () =>
            {
                _fixture.GetWebFormWithTask();
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = "WebTransaction", CallCountAllHarvests = 1 },
            new() { metricName = "External/all", CallCountAllHarvests = 1},
            new() { metricName = "External/allWeb", CallCountAllHarvests = 1},
            new() { metricName = "External/google.com/all", CallCountAllHarvests = 1},
            new() { metricName = "External/google.com/Stream/GET", CallCountAllHarvests = 1},
        };

        var endpointMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = "WebTransaction/ASP/webformwithtask.aspx", callCount = 1 },
            new() { metricName = "External/google.com/Stream/GET", CallCountAllHarvests = 1, metricScope = "WebTransaction/ASP/webformwithtask.aspx"}
        };
        expectedMetrics.AddRange(endpointMetrics);

        var expectedTransactionTraceSegments = new List<string>
            {
                "ExecuteRequestHandler",
                "webformwithtask.aspx",
                "External/google.com/Stream/GET"
            };
        var expectedTransactionTraceAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200" },
                { "http.statusCode", 200 }
            };
        var expectedTransactionEventAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200"},
                { "http.statusCode", 200 }
            };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var transactionSample = _fixture.AgentLog
            .GetTransactionSamples()
            .FirstOrDefault(sample => sample.Path == "WebTransaction/ASP/webformwithtask.aspx");

        //order transactions chronologically
        var selectedTransactionEvent = _fixture.AgentLog.GetTransactionEvents()
            .Where(transactionEvent => transactionEvent != null
                && transactionEvent.IntrinsicAttributes != null
                && transactionEvent.IntrinsicAttributes.ContainsKey("timestamp"))
            .OrderBy(transactionEvent => transactionEvent.IntrinsicAttributes["timestamp"])
            .FirstOrDefault();

        Assert.Multiple(
            () => Assert.NotNull(transactionSample),
            () => Assert.NotNull(selectedTransactionEvent)
        );

        Assert.Multiple
        (
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
            () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
            () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, selectedTransactionEvent)
        );

    }
}
