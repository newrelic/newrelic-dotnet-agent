// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.MassTransit;

/// <summary>
/// A consumer that throws must still end its consume transaction. If it doesn't, the transaction is only
/// reported when the GC finalizes it, which the fixture's known-problems check fails on
/// (AgentLogBase.TransactionEndedByGCFinalizerLogLineRegEx).
/// </summary>
public abstract class MassTransitConsumerErrorTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private const string ConsumeTransactionNamePrefix = "OtherTransaction/Message/MassTransit/Queue/Named/";
    private const string ExceptionMessagePrefix = "MassTransit consumer failure: ";

    private readonly TFixture _fixture;

    protected MassTransitConsumerErrorTestsBase(TFixture fixture, ITestOutputHelper output, bool useStartBus) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        _fixture.AddCommand(useStartBus ? "MassTransitExerciser StartBus" : "MassTransitExerciser StartHost");
        _fixture.AddCommand("MassTransitExerciser PublishFailing failingMessageOne");
        _fixture.AddCommand("MassTransitExerciser PublishFailing failingMessageTwo");
        _fixture.AddCommand(useStartBus ? "MassTransitExerciser StopBus" : "MassTransitExerciser StopHost");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var consumeMetricNameRegex = @"MessageBroker\/MassTransit\/Queue\/Consume\/Named\/([^\/]+)";
        var expectedMetrics = new[]
        {
            new Assertions.ExpectedMetric { metricName = consumeMetricNameRegex, CallCountAllHarvests = 2, IsRegexName = true, metricScope = @"OtherTransaction\/Message\/MassTransit\/Queue\/Named\/([^\/]+)", IsRegexScope = true },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var consumeTransactionEvents = _fixture.AgentLog.GetTransactionEvents()
            .Where(e => e.IntrinsicAttributes["name"].ToString().StartsWith(ConsumeTransactionNamePrefix))
            .ToList();

        var errorEvents = _fixture.AgentLog.GetErrorEvents()
            .Where(e => e.IntrinsicAttributes["transactionName"].ToString().StartsWith(ConsumeTransactionNamePrefix))
            .ToList();

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assert.Equal(2, consumeTransactionEvents.Count),
            () => Assert.All(consumeTransactionEvents, e => Assert.Equal(true, e.IntrinsicAttributes["error"])),
            () => Assert.Equal(2, errorEvents.Count),
            () => Assert.All(errorEvents, e => Assert.Equal("System.InvalidOperationException", e.IntrinsicAttributes["error.class"].ToString())),
            () => Assert.All(errorEvents, e => Assert.StartsWith(ExceptionMessagePrefix, e.IntrinsicAttributes["error.message"].ToString()))
        );
    }
}

// Tests using StartHost (hosted service configuration method)
[Trait("Platform", "WindowsOnly")]
public class MassTransitConsumerErrorTests_StartHost_FW462 : MassTransitConsumerErrorTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public MassTransitConsumerErrorTests_StartHost_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output, false)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class MassTransitConsumerErrorTests_StartHost_FWLatest : MassTransitConsumerErrorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MassTransitConsumerErrorTests_StartHost_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false)
    {
    }
}

public class MassTransitConsumerErrorTests_StartHost_CoreOldest : MassTransitConsumerErrorTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MassTransitConsumerErrorTests_StartHost_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, false)
    {
    }
}

public class MassTransitConsumerErrorTests_StartHost_CoreLatest : MassTransitConsumerErrorTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MassTransitConsumerErrorTests_StartHost_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false)
    {
    }
}

// Tests using StartBus (bus factory configuration method)
[Trait("Platform", "WindowsOnly")]
public class MassTransitConsumerErrorTests_StartBus_FW462 : MassTransitConsumerErrorTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public MassTransitConsumerErrorTests_StartBus_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output, true)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class MassTransitConsumerErrorTests_StartBus_FWLatest : MassTransitConsumerErrorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MassTransitConsumerErrorTests_StartBus_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true)
    {
    }
}

public class MassTransitConsumerErrorTests_StartBus_CoreOldest : MassTransitConsumerErrorTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MassTransitConsumerErrorTests_StartBus_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, true)
    {
    }
}

public class MassTransitConsumerErrorTests_StartBus_CoreLatest : MassTransitConsumerErrorTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MassTransitConsumerErrorTests_StartBus_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true)
    {
    }
}
