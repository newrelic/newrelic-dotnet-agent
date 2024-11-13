// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

public abstract class LinuxKafkaTest<T> : NewRelicIntegrationTest<T> where T : KafkaTestFixtureBase
{
    private const int TopicNameLength = 15;

    private readonly string _topicName;
    private readonly T _fixture;
    private string _bootstrapServer;

    protected LinuxKafkaTest(T fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _topicName = GenerateTopic();

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("debug");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.LogToConsole();

                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_KAFKA_TOPIC", _topicName);
            },
            exerciseApplication: () =>
            {
                _fixture.Delay(15); // wait long enough to ensure kafka and app are ready
                _fixture.ExerciseApplication();

                _bootstrapServer = _fixture.GetBootstrapServer();

                _fixture.Delay(11); // wait long enough to ensure a metric harvest occurs after we exercise the app
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromSeconds(11));

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var messageBrokerProduce = "MessageBroker/Kafka/Topic/Produce/Named/" + _topicName;
        var messageBrokerProduceSerializationKey = messageBrokerProduce + "/Serialization/Key";
        var messageBrokerProduceSerializationValue = messageBrokerProduce + "/Serialization/Value";

        var messageBrokerConsume = "MessageBroker/Kafka/Topic/Consume/Named/" + _topicName;

        var consumeTransactionName = @"OtherTransaction/Message/Kafka/Topic/Consume/Named/" + _topicName;
        var produceWebTransactionName = @"WebTransaction/MVC/Kafka/Produce";

        var messageBrokerNode = $"MessageBroker/Kafka/Nodes/{_bootstrapServer}";
        var messageBrokerNodeProduceTopic = $"MessageBroker/Kafka/Nodes/{_bootstrapServer}/Produce/{_topicName}";
        var messageBrokerNodeConsumeTopic = $"MessageBroker/Kafka/Nodes/{_bootstrapServer}/Consume/{_topicName}";

        var metrics = _fixture.AgentLog.GetMetrics();
        var spans = _fixture.AgentLog.GetSpanEvents();
        var produceSpan = spans.FirstOrDefault(s => s.IntrinsicAttributes["name"].Equals(messageBrokerProduce));
        var consumeTxnSpan = spans.FirstOrDefault(s => s.IntrinsicAttributes["name"].Equals(consumeTransactionName));

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = produceWebTransactionName, callCount = 2 }, // includes sync and async actions
            new() { metricName = messageBrokerProduce, callCount = 2 },
            new() { metricName = messageBrokerProduce, metricScope = produceWebTransactionName, callCount = 2 },
            new() { metricName = messageBrokerProduceSerializationKey, callCount = 2 },
            new() { metricName = messageBrokerProduceSerializationKey, metricScope = produceWebTransactionName, callCount = 2 },
            new() { metricName = messageBrokerProduceSerializationValue, callCount = 2 },
            new() { metricName = messageBrokerProduceSerializationValue, metricScope = produceWebTransactionName, callCount = 2 },

            new() { metricName = consumeTransactionName, callCount = 2 },
            new() { metricName = messageBrokerConsume, callCount = 2 },
            new() { metricName = messageBrokerConsume, metricScope = consumeTransactionName, callCount = 2 },
            new() { metricName = "Supportability/TraceContext/Create/Success", callCount = 2 },
            new() { metricName = "Supportability/TraceContext/Accept/Success", callCount = 2 },

            new() { metricName = messageBrokerNode, callCount = 4 },
            new() { metricName = messageBrokerNodeProduceTopic, callCount = 2 },
            new() { metricName = messageBrokerNodeConsumeTopic, callCount = 2 }
        };

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assert.True(produceSpan.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(produceSpan.IntrinsicAttributes.ContainsKey("parentId")),
            () => Assert.NotNull(consumeTxnSpan),
            () => Assert.True(consumeTxnSpan.UserAttributes.ContainsKey("kafka.consume.byteCount")),
            () => Assert.InRange((long)consumeTxnSpan.UserAttributes["kafka.consume.byteCount"], 450, 470), // includes headers
            () => Assert.True(consumeTxnSpan.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(consumeTxnSpan.IntrinsicAttributes.ContainsKey("parentId"))
        );
    }

    internal static string GenerateTopic()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < TopicNameLength; i++)
        {
            var shifter= RandomNumberGenerator.GetInt32(0, 26);
            builder.Append(Convert.ToChar(shifter + 65));
        }

        return builder.ToString();
    }
}

public class KafkaDotNet8Test : LinuxKafkaTest<KafkaDotNet8TestFixture>
{
    public KafkaDotNet8Test(KafkaDotNet8TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class KafkaDotNet9Test : LinuxKafkaTest<KafkaDotNet9TestFixture>
{
    public KafkaDotNet9Test(KafkaDotNet9TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
