// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Blazor;

public class SignalRHubTests : NewRelicIntegrationTest<RemoteServiceFixtures.BlazorSignalRApplicationFixture>
{
    private readonly RemoteServiceFixtures.BlazorSignalRApplicationFixture _fixture;

    public SignalRHubTests(RemoteServiceFixtures.BlazorSignalRApplicationFixture fixture, ITestOutputHelper output)
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
                configModifier.SetLogLevel("finest");
                configModifier.ForceTransactionTraces();
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
            },
            exerciseApplication: () =>
            {
                _fixture.InvokeSignalRHub("Hello Hub");
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"OtherTransaction/Custom/BlazorSignalRApplication.Hubs.EchoHub/SendEcho", callCount = 1 },
            new() { metricName = @"DotNet/BlazorSignalRApplication.Hubs.EchoHub/SendEcho", callCount = 1 },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var webSocketLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.WebSocketHandshakeDetectedLogLineRegex);

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assert.NotNull(webSocketLogLine)
        );
    }
}
