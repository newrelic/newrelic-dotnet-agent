// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.AzureServiceBus;

public abstract class AzureServiceBusTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private readonly string _queueName;

    protected AzureServiceBusTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(1));
        _fixture.TestLogger = output;

        _queueName = $"test-queue-{Guid.NewGuid()}";

        _fixture.AddCommand($"AzureServiceBusExerciser InitializeQueue {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser ExerciseMultipleReceiveOperationsOnAMessage {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser ScheduleAndReceiveAMessage {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser ReceiveAndDeadLetterAMessage {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser ReceiveAndAbandonAMessage {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser DeleteQueue {_queueName}");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                configModifier
                    .SetLogLevel("finest")
                    .EnableDistributedTrace()
                    .ForceTransactionTraces()
                    .ConfigureFasterMetricsHarvestCycle(20)
                    .ConfigureFasterSpanEventsHarvestCycle(20)
                    .ConfigureFasterTransactionTracesHarvestCycle(25)
                    ;
            }
        );

        _fixture.Initialize();
    }

    private readonly string _metricScopeBase = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.AzureServiceBus.AzureServiceBusExerciser";

    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Produce/Named/{_queueName}", callCount = 4},
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Produce/Named/{_queueName}", callCount = 1, metricScope = $"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessage"},
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Produce/Named/{_queueName}", callCount = 1, metricScope = $"{_metricScopeBase}/ScheduleAndReceiveAMessage"},
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Produce/Named/{_queueName}", callCount = 1, metricScope = $"{_metricScopeBase}/ReceiveAndDeadLetterAMessage"},
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Produce/Named/{_queueName}", callCount = 1, metricScope = $"{_metricScopeBase}/ReceiveAndAbandonAMessage"},

            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Consume/Named/{_queueName}", callCount = 10},
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Consume/Named/{_queueName}", callCount = 5, metricScope = $"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessage"},
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Consume/Named/{_queueName}", callCount = 1, metricScope = $"{_metricScopeBase}/ScheduleAndReceiveAMessage"},
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Consume/Named/{_queueName}", callCount = 1, metricScope = $"{_metricScopeBase}/ReceiveAndDeadLetterAMessage"},
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Consume/Named/{_queueName}", callCount = 3, metricScope = $"{_metricScopeBase}/ReceiveAndAbandonAMessage"},

            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Peek/Named/{_queueName}", callCount = 1 },
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Peek/Named/{_queueName}", callCount = 1, metricScope = $"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessage" },

            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Purge/Named/{_queueName}", callCount = 2 },
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Purge/Named/{_queueName}", callCount = 1, metricScope = $"{_metricScopeBase}/ReceiveAndDeadLetterAMessage" },
            new() { metricName = $"MessageBroker/AzureServiceBus/Queue/Purge/Named/{_queueName}", callCount = 1, metricScope = $"{_metricScopeBase}/ReceiveAndAbandonAMessage" },
        };

        var exerciseMultipleReceiveOperationsOnAMessageTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessage");
        var scheduleAndReceiveAMessageTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/ScheduleAndReceiveAMessage");

        var expectedTransactionTraceSegments = new List<string>
        {
            $"MessageBroker/AzureServiceBus/Queue/Consume/Named/{_queueName}"
        };

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"{_metricScopeBase}/ScheduleAndReceiveAMessage"); // this will always be the slowest transaction

        var queueProduceSpanEvents = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/AzureServiceBus/Queue/Produce/Named/{_queueName}");
        var queueConsumeSpanEvents = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/AzureServiceBus/Queue/Consume/Named/{_queueName}");
        var queuePeekSpanEvents = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/AzureServiceBus/Queue/Peek/Named/{_queueName}");
        var queuePurgeSpanEvents = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/AzureServiceBus/Queue/Purge/Named/{_queueName}");

        var expectedProduceAgentAttributes = new List<string>
        {
            "server.address",
            "messaging.destination.name",
        };

        var expectedConsumeAgentAttributes = new List<string>
        {
            "server.address",
            "messaging.destination.name",
        };


        var expectedPeekAgentAttributes = new List<string>
        {
            "server.address",
            "messaging.destination.name",
        };

        var expectedPurgeAgentAttributes = new List<string>
        {
            "server.address",
            "messaging.destination.name",
        };

        var expectedIntrinsicAttributes = new List<string> { "span.kind", };

        Assertions.MetricsExist(expectedMetrics, metrics);

        NrAssert.Multiple(
            () => Assert.True(exerciseMultipleReceiveOperationsOnAMessageTransactionEvent != null, "ExerciseMultipleReceiveOperationsOnAMessageTransactionEvent should not be null"),
            () => Assert.True(scheduleAndReceiveAMessageTransactionEvent != null, "ScheduleAndReceiveAMessageTransactionEvent should not be null"),
            () => Assert.True(transactionSample != null, "transactionSample should not be null"),
            () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),

            () => Assertions.SpanEventHasAttributes(expectedProduceAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueProduceSpanEvents),
            () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, queueProduceSpanEvents),

            () => Assertions.SpanEventHasAttributes(expectedConsumeAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueConsumeSpanEvents),
            () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, queueConsumeSpanEvents),

            () => Assertions.SpanEventHasAttributes(expectedPeekAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queuePeekSpanEvents),

            () => Assertions.SpanEventHasAttributes(expectedPurgeAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queuePurgeSpanEvents)
        );
    }
}

[NetFrameworkTest]
public class AzureServiceBusTestsFWLatest : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public AzureServiceBusTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[NetFrameworkTest]
public class AzureServiceBusTestsFW462 : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public AzureServiceBusTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[NetCoreTest]
public class AzureServiceBusTestsCoreOldest : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public AzureServiceBusTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[NetCoreTest]
public class AzureServiceBusTestsCoreLatest : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public AzureServiceBusTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
