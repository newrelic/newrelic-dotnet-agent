// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

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

    private readonly string _metricNameBase = "MessageBroker/AzureServiceBus/Queue/Consume/Named";
    private readonly string _transactionNameBase = "OtherTransaction/Message/AzureServiceBus/Queue/Named";

    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = $"{_metricNameBase}/{_queueName}", callCount = 2},
            new() { metricName = $"{_metricNameBase}/{_queueName}", callCount = 2, metricScope = $"{_transactionNameBase}/{_queueName}"},
        };

        var expectedTransactinEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_transactionNameBase}/{_queueName}");

        var expectedTransactionTraceSegments = new List<string>
        {
            $"{_metricNameBase}/{_queueName}"
        };

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"{_transactionNameBase}/{_queueName}");

        var queueConsumeSpanEvents = _fixture.AgentLog.TryGetSpanEvent($"{_metricNameBase} /{_queueName}");

        var expectedConsumeAgentAttributes = new List<string>
        {
            "server.address",
            "messaging.destination.name",
        };

        var expectedIntrinsicAttributes = new List<string> { "span.kind", };

        Assertions.MetricsExist(expectedMetrics, metrics);

        NrAssert.Multiple(
            () => Assert.True(expectedTransactinEvent != null, "expectedTransactionEvent should not be null"),
            () => Assert.True(transactionSample != null, "transactionSample should not be null"),
            () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),

            () => Assertions.SpanEventHasAttributes(expectedConsumeAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueConsumeSpanEvents),
            () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, queueConsumeSpanEvents)
        );
    }
}

[NetFrameworkTest]
public class AzureServiceBusProcessorTestsFWLatest : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public AzureServiceBusProcessorTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[NetFrameworkTest]
public class AzureServiceBusProcessorTestsFW462 : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public AzureServiceBusProcessorTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[NetCoreTest]
public class AzureServiceBusProcessorTestsCoreOldest : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public AzureServiceBusProcessorTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[NetCoreTest]
public class AzureServiceBusProcessorTestsCoreLatest : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public AzureServiceBusProcessorTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
