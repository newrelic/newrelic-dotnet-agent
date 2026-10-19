// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Blazor;

public class BlazorServerLongPollingInteractivityTests : NewRelicIntegrationTest<RemoteServiceFixtures.BlazorSignalRLongPollingApplicationFixture>
{
    private readonly RemoteServiceFixtures.BlazorSignalRLongPollingApplicationFixture _fixture;

    public BlazorServerLongPollingInteractivityTests(RemoteServiceFixtures.BlazorSignalRLongPollingApplicationFixture fixture, ITestOutputHelper output)
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
                _fixture.CloseBrowser();
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
            new() { metricName = @"OtherTransaction/Custom/BlazorSignalRApplication.Components.Pages.Home/SendPhrase", CallCountAllHarvests = 1 },
            new() { metricName = @"DotNet/BlazorSignalRApplication.Components.Pages.Home/SendPhrase", CallCountAllHarvests = 1 },
        };

        var unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"WebTransaction/ASP/_blazor" },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var webSocketLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.WebSocketHandshakeDetectedLogLineRegex);
        var signalRLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.SignalRTransportRequestIgnoredLogLineRegex);

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
            () => Assert.NotNull(signalRLogLine),
            () => Assert.Null(webSocketLogLine)
        );
    }
}
