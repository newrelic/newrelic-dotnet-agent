// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http.Connections;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Blazor;

public abstract class SignalRHubTests : NewRelicIntegrationTest<RemoteServiceFixtures.BlazorSignalRApplicationFixture>
{
    protected readonly RemoteServiceFixtures.BlazorSignalRApplicationFixture _fixture;

    protected SignalRHubTests(RemoteServiceFixtures.BlazorSignalRApplicationFixture fixture, ITestOutputHelper output, HttpTransportType transport)
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
                _fixture.InvokeSignalRHub("Hello Hub", transport);
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );
        _fixture.Initialize();
    }

    protected static List<Assertions.ExpectedMetric> SendEchoMetrics { get; } = new()
    {
        new() { metricName = @"OtherTransaction/Custom/BlazorSignalRApplication.Hubs.EchoHub/SendEcho", CallCountAllHarvests = 1 },
        new() { metricName = @"DotNet/BlazorSignalRApplication.Hubs.EchoHub/SendEcho", CallCountAllHarvests = 1 },
    };
}

public class SignalRHubWebSocketsTests : SignalRHubTests
{
    public SignalRHubWebSocketsTests(RemoteServiceFixtures.BlazorSignalRApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, HttpTransportType.WebSockets)
    {
    }

    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var webSocketLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.WebSocketHandshakeDetectedLogLineRegex);

        NrAssert.Multiple(
            () => Assertions.MetricsExist(SendEchoMetrics, metrics),
            () => Assert.NotNull(webSocketLogLine)
        );
    }
}

public abstract class SignalRHubIgnoredTransportTests : SignalRHubTests
{
    protected SignalRHubIgnoredTransportTests(RemoteServiceFixtures.BlazorSignalRApplicationFixture fixture, ITestOutputHelper output, HttpTransportType transport)
        : base(fixture, output, transport)
    {
    }

    protected abstract bool ExpectServerSentEventsSkip { get; }

    [Fact]
    public void Test()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>(SendEchoMetrics)
        {
            new() { metricName = @"WebTransaction/ASP/echohub/negotiate" },
        };

        var unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"WebTransaction/ASP/echohub" },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var webSocketLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.WebSocketHandshakeDetectedLogLineRegex);
        var signalRLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.SignalRTransportRequestIgnoredLogLineRegex);
        var sseLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.SignalRServerSentEventsDetectedLogLineRegex);

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
            () => Assert.NotNull(signalRLogLine),
            () => Assert.Null(webSocketLogLine),
            () => Assert.Equal(ExpectServerSentEventsSkip, sseLogLine != null)
        );
    }
}

public class SignalRHubLongPollingTests : SignalRHubIgnoredTransportTests
{
    public SignalRHubLongPollingTests(RemoteServiceFixtures.BlazorSignalRApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, HttpTransportType.LongPolling)
    {
    }

    protected override bool ExpectServerSentEventsSkip => false;
}

public class SignalRHubServerSentEventsTests : SignalRHubIgnoredTransportTests
{
    public SignalRHubServerSentEventsTests(RemoteServiceFixtures.BlazorSignalRApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, HttpTransportType.ServerSentEvents)
    {
    }

    protected override bool ExpectServerSentEventsSkip => true;
}
