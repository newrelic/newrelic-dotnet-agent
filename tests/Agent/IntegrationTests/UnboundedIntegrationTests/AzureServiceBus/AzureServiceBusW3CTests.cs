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

public abstract class AzureServiceBusW3CTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private readonly string _queueOrTopicName;
    private readonly string _destinationType;

    protected AzureServiceBusW3CTestsBase(TFixture fixture, string destinationType, ITestOutputHelper output) :
        base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(1));
        _fixture.TestLogger = output;

        _queueOrTopicName = $"test-queue-{Guid.NewGuid()}";
        _destinationType = destinationType;


        _fixture.AddCommand($"AzureServiceBusExerciser Initialize{_destinationType} {_queueOrTopicName}");
        // send and receive on separate transactions to validate DT propagation
        _fixture.AddCommand($"AzureServiceBusExerciser SendAMessageFor{_destinationType} {_queueOrTopicName}");
        _fixture.AddCommand($"AzureServiceBusExerciser ReceiveAMessageFor{_destinationType} {_queueOrTopicName}");
        _fixture.AddCommand($"AzureServiceBusExerciser Delete{_destinationType} {_queueOrTopicName}");

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
                    .ConfigureFasterTransactionTracesHarvestCycle(25);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    private readonly string _metricScopeBase =
        "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.AzureServiceBus.AzureServiceBusExerciser";

    [Fact]
    public void Test()
    {
        // attributes

        var sendTx =
            _fixture.AgentLog.TryGetTransactionEvent(
                $"{_metricScopeBase}/SendAMessageFor{_destinationType}");

        var receiveTx=
            _fixture.AgentLog.TryGetTransactionEvent(
                $"{_metricScopeBase}/ReceiveAMessageFor{_destinationType}");

        var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

        // produce is always queue.
        var produceSpans = spanEvents
            .Where(@event => @event.IntrinsicAttributes["name"].ToString()!
                .Contains("MessageBroker/ServiceBus/Queue/Produce/Named/")).ToList();

        var consumeSpans = spanEvents
            .Where(@event => @event.IntrinsicAttributes["name"].ToString()!
                .Contains($"MessageBroker/ServiceBus/{_destinationType}/Consume/Named/")).ToList();

        Assert.NotNull(sendTx);
        Assert.NotNull(receiveTx);
        Assert.NotNull(produceSpans);
        Assert.NotNull(consumeSpans);

        // DT propagation validation
        Assert.True(sendTx.IntrinsicAttributes.TryGetValue("traceId", out var sendTraceId));
        Assert.True(receiveTx.IntrinsicAttributes.TryGetValue("traceId", out var receiveTraceId));
        Assert.Equal(receiveTraceId, sendTraceId);

        Assert.True(sendTx.IntrinsicAttributes.TryGetValue("priority", out var sendPriority));
        Assert.True(receiveTx.IntrinsicAttributes.TryGetValue("priority", out var receivePriority));
        Assert.Equal(receivePriority.ToString().Substring(0, 7), sendPriority.ToString().Substring(0, 7)); // keep the values the same length

        Assert.True(sendTx.IntrinsicAttributes.TryGetValue("sampled", out var sendSampled));
        Assert.True(receiveTx.IntrinsicAttributes.TryGetValue("sampled", out var receiveSampled));
        Assert.Equal(receiveSampled, sendSampled);

        // validate spans have the same trace / priority / sampled values
        foreach (var produceSpan in produceSpans)
        {
            Assert.Equal(sendTx.IntrinsicAttributes["guid"], produceSpan.IntrinsicAttributes["transactionId"]);
            Assert.Equal(sendTx.IntrinsicAttributes["traceId"], produceSpan.IntrinsicAttributes["traceId"]);
            Assert.True(
                sendTx.IntrinsicAttributes["priority"].IsEqualTo(produceSpan.IntrinsicAttributes["priority"]),
                $"priority: expected: {sendTx.IntrinsicAttributes["priority"]}, actual: {produceSpan.IntrinsicAttributes["priority"]}");
        }

        foreach (var consumeSpan in consumeSpans)
        {
            Assert.Equal(receiveTx.IntrinsicAttributes["guid"], consumeSpan.IntrinsicAttributes["transactionId"]);
            Assert.Equal(receiveTx.IntrinsicAttributes["traceId"], consumeSpan.IntrinsicAttributes["traceId"]);
            Assert.True(
                receiveTx.IntrinsicAttributes["priority"].IsEqualTo(consumeSpan.IntrinsicAttributes["priority"]),
                $"priority: expected: {receiveTx.IntrinsicAttributes["priority"]}, actual: {consumeSpan.IntrinsicAttributes["priority"]}");
        }

        // metrics
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric
            {
                metricName = $"Supportability/DistributedTrace/CreatePayload/Success", callCount = 1
            },
            new Assertions.ExpectedMetric
            {
                metricName = $"Supportability/TraceContext/Create/Success", callCount = 1
            },
        };

        var metrics = _fixture.AgentLog.GetMetrics();
        Assertions.MetricsExist(expectedMetrics, metrics);
    }
}

#region Queue Tests

public class AzureServiceBusW3CQueueTestsFWLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public AzureServiceBusW3CQueueTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture,
        ITestOutputHelper output) : base(fixture, "Queue", output)
    {
    }
}

public class AzureServiceBusW3CQueueTestsFW462 : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public AzureServiceBusW3CQueueTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) :
        base(fixture, "Queue", output)
    {
    }
}

public class
    AzureServiceBusW3CQueueTestsCoreOldest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public AzureServiceBusW3CQueueTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture,
        ITestOutputHelper output) : base(fixture, "Queue", output)
    {
    }
}

public class
    AzureServiceBusW3CQueueTestsCoreLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public AzureServiceBusW3CQueueTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture,
        ITestOutputHelper output) : base(fixture, "Queue", output)
    {
    }
}

#endregion Queue Tests

#region Topic Tests

public class AzureServiceBusW3CTopicTestsFWLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public AzureServiceBusW3CTopicTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture,
        ITestOutputHelper output) : base(fixture, "Topic", output)
    {
    }
}

public class AzureServiceBusW3CTopicTestsFW462 : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public AzureServiceBusW3CTopicTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) :
        base(fixture, "Topic", output)
    {
    }
}

public class
    AzureServiceBusW3CTopicTestsCoreOldest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public AzureServiceBusW3CTopicTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture,
        ITestOutputHelper output) : base(fixture, "Topic", output)
    {
    }
}

public class
    AzureServiceBusW3CTopicTestsCoreLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public AzureServiceBusW3CTopicTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture,
        ITestOutputHelper output) : base(fixture, "Topic", output)
    {
    }
}

#endregion Topic Tests

#region DT Header Replacement Tests

/// <summary>
/// Verifies that when a ServiceBusMessage already has DT headers in ApplicationProperties,
/// the agent replaces them rather than duplicating or erroring.
/// </summary>
public abstract class AzureServiceBusW3CDTHeaderReplacementTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private readonly string _queueName;

    protected AzureServiceBusW3CDTHeaderReplacementTestsBase(TFixture fixture, ITestOutputHelper output) :
        base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(1));
        _fixture.TestLogger = output;

        _queueName = $"test-dtheader-{Guid.NewGuid()}";

        _fixture.AddCommand($"AzureServiceBusExerciser InitializeQueue {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser SendAndReceiveWithExistingDTHeadersForQueue {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser ReceiveAMessageForQueue {_queueName}");
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
                    .ConfigureFasterTransactionTracesHarvestCycle(25);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(1));
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
            new() { metricName = "Supportability/TraceContext/Create/Success", callCount = 1 },
            new() { metricName = "Supportability/TraceContext/Accept/Success", callCount = 1 },
        };

        Assertions.MetricsExist(expectedMetrics, metrics);

        // Verify DT propagation — stale headers should have been replaced
        var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();
        foreach (var span in spanEvents)
        {
            if (span.IntrinsicAttributes.TryGetValue("traceId", out var traceId))
            {
                Assert.NotEqual("stale0000000000000000000000000", traceId.ToString());
            }
        }
    }
}

public class AzureServiceBusW3CDTHeaderReplacementTestsCoreLatest : AzureServiceBusW3CDTHeaderReplacementTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public AzureServiceBusW3CDTHeaderReplacementTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture,
        ITestOutputHelper output) : base(fixture, output)
    {
    }
}

#endregion DT Header Replacement Tests
