// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Blazor;

public class BlazorServerInteractivityTests : NewRelicIntegrationTest<RemoteServiceFixtures.BlazorSignalRApplicationFixture>
{
    private readonly RemoteServiceFixtures.BlazorSignalRApplicationFixture _fixture;

    public BlazorServerInteractivityTests(RemoteServiceFixtures.BlazorSignalRApplicationFixture fixture, ITestOutputHelper output)
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
                _fixture.InvokeBlazorHub("Hello SignalR");
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
            new() { metricName = @"OtherTransaction/Custom/BlazorSignalRApplication.Components.Pages.Home/SendPhrase", callCount = 1 },
            new() { metricName = @"DotNet/BlazorSignalRApplication.Components.Pages.Home/SendPhrase", callCount = 1 },
        };

        var unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"WebTransaction/ASP/_blazor" },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var sendEchoMetrics = metrics
            .Where(m => m.MetricSpec.Name.Contains("SendEcho", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var webSocketLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.WebSocketHandshakeDetectedLogLineRegex);

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
            () => Assert.Empty(sendEchoMetrics),
            () => Assert.NotNull(webSocketLogLine)
        );
    }
}
