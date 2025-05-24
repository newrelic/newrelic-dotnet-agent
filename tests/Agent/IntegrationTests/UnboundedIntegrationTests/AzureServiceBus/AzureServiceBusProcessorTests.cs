// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.AzureServiceBus;

public abstract class AzureServiceBusProcessorTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private readonly string _queueName;

    protected AzureServiceBusProcessorTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(1));
        _fixture.TestLogger = output;

        _queueName = $"test-queue-{Guid.NewGuid()}";

        _fixture.AddCommand($"AzureServiceBusExerciser InitializeQueue {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser ExerciseServiceBusProcessor {_queueName}");
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
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    private readonly string _consumeMetricNameBase = "MessageBroker/ServiceBus/Queue/Consume/Named";
    private readonly string _processMetricNameBase = "MessageBroker/ServiceBus/Queue/Process/Named";
    private readonly string _settleMetricNameBase = "MessageBroker/ServiceBus/Queue/Settle/Named";
    private readonly string _transactionNameBase = "OtherTransaction/Message/ServiceBus/Queue/Named";

    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        // 2 messages, 1 consume segment, 1 process segment, 1 settle segment per message
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = $"{_consumeMetricNameBase}/{_queueName}", callCount = 2},
            new() { metricName = $"{_consumeMetricNameBase}/{_queueName}", callCount = 2, metricScope = $"{_transactionNameBase}/{_queueName}"},
            new() { metricName = $"{_processMetricNameBase}/{_queueName}", callCount = 2, metricScope = $"{_transactionNameBase}/{_queueName}"},
            new() { metricName = $"{_settleMetricNameBase}/{_queueName}", callCount = 2, metricScope = $"{_transactionNameBase}/{_queueName}"},
        };

        var expectedTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_transactionNameBase}/{_queueName}");

        var expectedTransactionTraceSegments = new List<string>
        {
            $"{_consumeMetricNameBase}/{_queueName}",
            $"{_processMetricNameBase}/{_queueName}",
            "DotNet/ServiceBusProcessor/OnProcessMessageAsync",
            $"{_settleMetricNameBase}/{_queueName}",
        };

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"{_transactionNameBase}/{_queueName}");

        var queueConsumeSpanEvent = _fixture.AgentLog.TryGetSpanEvent($"{_consumeMetricNameBase}/{_queueName}");
        var queueProcessSpanEvent = _fixture.AgentLog.TryGetSpanEvent($"{_processMetricNameBase}/{_queueName}");

        var expectedConsumeAgentAttributes = new List<string>
        {
            "server.address",
            "messaging.destination.name",
        };

        var expectedIntrinsicAttributes = new List<string> { "span.kind", };

        Assertions.MetricsExist(expectedMetrics, metrics);

        NrAssert.Multiple(
            () => Assert.NotNull(expectedTransactionEvent),
            () => Assert.NotNull(transactionSample),
            () => Assert.NotNull(queueConsumeSpanEvent),
            () => Assert.NotNull(queueProcessSpanEvent),
            () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),

            () => Assertions.SpanEventHasAttributes(expectedConsumeAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueConsumeSpanEvent),
            () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, queueConsumeSpanEvent),
            () => Assertions.SpanEventHasAttributes(expectedConsumeAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueConsumeSpanEvent),
            () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, queueConsumeSpanEvent)
        );
    }
}

public class AzureServiceBusProcessorTestsFWLatest : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public AzureServiceBusProcessorTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AzureServiceBusProcessorTestsFW462 : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public AzureServiceBusProcessorTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AzureServiceBusProcessorTestsCoreOldest : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public AzureServiceBusProcessorTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AzureServiceBusProcessorTestsCoreLatest : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public AzureServiceBusProcessorTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
