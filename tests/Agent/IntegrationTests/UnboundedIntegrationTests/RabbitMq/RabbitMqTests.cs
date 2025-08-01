// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;


namespace NewRelic.Agent.UnboundedIntegrationTests.RabbitMq;

public abstract class RabbitMqTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    private readonly ConsoleDynamicMethodFixture _fixture;

    private readonly string _sendReceiveQueue = $"integrationTestQueue-{Guid.NewGuid()}";
    private readonly string _purgeQueue = $"integrationPurgeTestQueue-{Guid.NewGuid()}";
    private readonly string _testExchangeName = $"integrationTestExchange-{Guid.NewGuid()}";
    // The topic name has to contain a '.' character.  See https://www.rabbitmq.com/tutorials/tutorial-five-dotnet.html
    private readonly string _sendReceiveTopic = "SendReceiveTopic.Topic";

    private readonly string _metricScopeBase = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.RabbitMQ.RabbitMQModernExerciser";

    protected RabbitMqTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;


        _fixture.AddCommand("RabbitMQModernExerciser ConnectAsync");
        _fixture.AddCommand($"RabbitMQModernExerciser SendReceiveAsync {_sendReceiveQueue} TestMessage");
        _fixture.AddCommand($"RabbitMQModernExerciser SendReceiveTempQueueAsync TempQueueTestMessage");
        _fixture.AddCommand($"RabbitMQModernExerciser QueuePurgeAsync {_purgeQueue}");
        _fixture.AddCommand($"RabbitMQModernExerciser SendReceiveTopicAsync {_testExchangeName} {_sendReceiveTopic} TopicTestMessage");
        // This is needed to avoid a hang on shutdown in the test app
        _fixture.AddCommand("RabbitMQModernExerciser ShutdownAsync");

        // AddActions() executes the applied actions after actions defined by the base.
        // In this case the base defines an exerciseApplication action we want to wait after.
        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.ForceTransactionTraces();

                configModifier.EnableOTelBridge(true);
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
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_sendReceiveQueue}", callCount = 1},
            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_sendReceiveQueue}", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveAsync"},

            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}", callCount = 1},
            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveAsync"},

            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_purgeQueue}", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_purgeQueue}", callCount = 1, metricScope = $"{_metricScopeBase}/QueuePurgeAsync" },

            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Purge/Named/{_purgeQueue}", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Purge/Named/{_purgeQueue}", callCount = 1, metricScope = $"{_metricScopeBase}/QueuePurgeAsync" },

            new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Produce/Temp", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Produce/Temp", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveTempQueueAsync"},

            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Topic/Produce/Named/{_sendReceiveTopic}", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Topic/Produce/Named/{_sendReceiveTopic}", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveTopicAsync" },

            new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Consume/Temp", callCount = 2 },
            new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Consume/Temp", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveTempQueueAsync"},
            new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Consume/Temp", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveTopicAsync" },
        };

        var sendReceiveTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SendReceiveAsync");
        var sendReceiveTempQueueTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SendReceiveTempQueueAsync");
        var queuePurgeTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/QueuePurgeAsync");
        var sendReceiveTopicTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SendReceiveTopicAsync");

        var expectedTransactionTraceSegments = new List<string>
        {
            $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}"
        };

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"{_metricScopeBase}/SendReceiveAsync");

        var queueProduceSpanEvents = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/RabbitMQ/Queue/Produce/Named/{_sendReceiveQueue}");
        var queueConsumeSpanEvents = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}");
        var purgeProduceSpanEvents = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/RabbitMQ/Queue/Produce/Named/{_purgeQueue}");
        var tempProduceSpanEvents = _fixture.AgentLog.TryGetSpanEvent(@"MessageBroker/RabbitMQ/Queue/Produce/Temp");
        var tempConsumeSpanEvents = _fixture.AgentLog.TryGetSpanEvent(@"MessageBroker/RabbitMQ/Queue/Consume/Temp");
        var topicProduceSpanEvents = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/RabbitMQ/Topic/Produce/Named/{_sendReceiveTopic}");

        var expectedProduceAgentAttributes = new List<string>
        {
            "server.address",
            "server.port",
            "messaging.destination.name",
            "message.routingKey",
            "messaging.rabbitmq.destination.routing_key"
        };

        var expectedTempProduceAgentAttributes = new List<string>
        {
            "server.address",
            "server.port",
            "message.routingKey",
            "messaging.rabbitmq.destination.routing_key"
        };

        var expectedConsumeAgentAttributes = new List<string>
        {
            "server.address",
            "server.port",
            "messaging.destination.name",
            "message.queueName",
            "messaging.destination_publish.name",
        };

        var expectedTempConsumeAgentAttributes = new List<string>
        {
            "server.address",
            "server.port",
        };

        var expectedIntrinsicAttributes = new List<string> { "span.kind", };

        Assertions.MetricsExist(expectedMetrics, metrics);

        NrAssert.Multiple(
            () => Assert.True(sendReceiveTransactionEvent != null, "sendReceiveTransactionEvent should not be null"),
            () => Assert.True(sendReceiveTempQueueTransactionEvent != null, "sendReceiveTempQueueTransactionEvent should not be null"),
            () => Assert.True(queuePurgeTransactionEvent != null, "queuePurgeTransactionEvent should not be null"),
            () => Assert.True(sendReceiveTopicTransactionEvent != null, "sendReceiveTopicTransactionEvent should not be null"),
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

            () => Assertions.SpanEventHasAttributes(expectedProduceAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, purgeProduceSpanEvents),
            () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, purgeProduceSpanEvents),

            () => Assertions.SpanEventHasAttributes(expectedTempProduceAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, tempProduceSpanEvents),
            () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, tempProduceSpanEvents),

            () => Assertions.SpanEventHasAttributes(expectedTempConsumeAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, tempConsumeSpanEvents),
            () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, tempConsumeSpanEvents),

            () => Assertions.SpanEventHasAttributes(expectedProduceAgentAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, topicProduceSpanEvents),
            () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, topicProduceSpanEvents)
        );
    }
}

public class RabbitMqTestsCoreLatest : RabbitMqTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public RabbitMqTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
