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

                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_KAFKA_TOPIC", _topicName);
            },
            exerciseApplication: () =>
            {
                _fixture.Delay(15); // wait long enough to ensure kafka and app are ready
                _fixture.TestLogger.WriteLine("Starting exercise application");
                _fixture.ExerciseApplication();

                _bootstrapServer = _fixture.GetBootstrapServer();

                _fixture.TestLogger.WriteLine("Waiting for metrics to be harvested");
                _fixture.Delay(30); // wait long enough to ensure a metric harvest occurs after we exercise the app
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromSeconds(11));

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

        var consumeWithTimeoutTransactionName = @"OtherTransaction/Custom/KafkaTestApp.Consumer/ConsumeOneWithTimeoutAsync";
        var consumeWithCancellationTransactionName = @"OtherTransaction/Custom/KafkaTestApp.Consumer/ConsumeOneWithCancellationTokenAsync";
        var produceWebTransactionName = @"WebTransaction/MVC/Kafka/Produce";
        var produceWithExistingHeadersWebTransactionName = @"WebTransaction/MVC/Kafka/ProduceAsyncWithExistingHeaders";

        var messageBrokerNode = $"MessageBroker/Kafka/Nodes/{_bootstrapServer}";
        var messageBrokerNodeProduceTopic = $"MessageBroker/Kafka/Nodes/{_bootstrapServer}/Produce/{_topicName}";
        var messageBrokerNodeConsumeTopic = $"MessageBroker/Kafka/Nodes/{_bootstrapServer}/Consume/{_topicName}";

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        

        var spans = _fixture.AgentLog.GetSpanEvents();
        var produceSpan = spans.FirstOrDefault(s => s.IntrinsicAttributes["name"].Equals(messageBrokerProduce));
        var consumeWithTimeoutTxnSpan = spans.FirstOrDefault(s => s.IntrinsicAttributes["name"].Equals(consumeWithTimeoutTransactionName));
        var consumeWithCancellationTxnSpan = spans.FirstOrDefault(s => s.IntrinsicAttributes["name"].Equals(consumeWithCancellationTransactionName));

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = produceWebTransactionName, CallCountAllHarvests = 4 }, // includes sync and async actions
            new() { metricName = produceWithExistingHeadersWebTransactionName, CallCountAllHarvests = 1 }, // produce with pre-existing DT headers
            new() { metricName = messageBrokerProduce, CallCountAllHarvests = 5 }, // 4 normal + 1 with existing headers
            new() { metricName = messageBrokerProduce, metricScope = produceWebTransactionName, CallCountAllHarvests = 4 },
            new() { metricName = messageBrokerProduce, metricScope = produceWithExistingHeadersWebTransactionName, CallCountAllHarvests = 1 },
            new() { metricName = messageBrokerProduceSerializationKey, CallCountAllHarvests = 5 },
            new() { metricName = messageBrokerProduceSerializationKey, metricScope = produceWebTransactionName, CallCountAllHarvests = 4 },
            new() { metricName = messageBrokerProduceSerializationValue, CallCountAllHarvests = 5 },
            new() { metricName = messageBrokerProduceSerializationValue, metricScope = produceWebTransactionName, CallCountAllHarvests = 4 },

            new() { metricName = consumeWithTimeoutTransactionName, CallCountAllHarvests = 3 }, // 2 normal + 1 for existing headers
            new() { metricName = consumeWithCancellationTransactionName, CallCountAllHarvests = 2 },
            new() { metricName = messageBrokerConsume, CallCountAllHarvests = 5 }, // 4 normal + 1 for existing headers
            new() { metricName = messageBrokerConsume, metricScope = consumeWithTimeoutTransactionName, CallCountAllHarvests = 3 },
            new() { metricName = messageBrokerConsume, metricScope = consumeWithCancellationTransactionName, CallCountAllHarvests = 2 },
            new() { metricName = "Supportability/TraceContext/Create/Success", CallCountAllHarvests = 5 }, // 4 normal + 1 with existing headers replaced
            new() { metricName = "Supportability/TraceContext/Accept/Success", CallCountAllHarvests = 5 }, // 4 normal + 1 with existing headers replaced

            new() { metricName = messageBrokerNode, CallCountAllHarvests = 10 }, // 8 normal + 2 for existing headers (produce+consume)
            new() { metricName = messageBrokerNodeProduceTopic, CallCountAllHarvests = 5 },
            new() { metricName = messageBrokerNodeConsumeTopic, CallCountAllHarvests = 5 }
        };

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assert.True(produceSpan.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(produceSpan.IntrinsicAttributes.ContainsKey("parentId")),
            () => Assert.NotNull(consumeWithTimeoutTxnSpan),
            () => Assert.True(consumeWithTimeoutTxnSpan.UserAttributes.ContainsKey("kafka.consume.byteCount")),
            () => Assert.InRange((long)consumeWithTimeoutTxnSpan.UserAttributes["kafka.consume.byteCount"], 450, 500), // includes headers; upper bound accommodates existing-headers message
            () => Assert.True(consumeWithTimeoutTxnSpan.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(consumeWithTimeoutTxnSpan.IntrinsicAttributes.ContainsKey("parentId")),
            () => Assert.NotNull(consumeWithCancellationTxnSpan),
            () => Assert.True(consumeWithCancellationTxnSpan.UserAttributes.ContainsKey("kafka.consume.byteCount")),
            () => Assert.InRange((long)consumeWithCancellationTxnSpan.UserAttributes["kafka.consume.byteCount"], 450, 500), // includes headers; upper bound accommodates potential race with existing-headers message
            () => Assert.True(consumeWithCancellationTxnSpan.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(consumeWithCancellationTxnSpan.IntrinsicAttributes.ContainsKey("parentId"))
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

[Collection("KafkaTests")]
[Trait("Architecture", "amd64")]
[Trait("Distro", "Ubuntu")]
public class KafkaDotNet8Test : LinuxKafkaTest<KafkaDotNet8TestFixture>
{
    public KafkaDotNet8Test(KafkaDotNet8TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[Collection("KafkaTests")]
[Trait("Architecture", "amd64")]
[Trait("Distro", "Ubuntu")]
public class KafkaDotNet10Test : LinuxKafkaTest<KafkaDotNet10TestFixture>
{
    public KafkaDotNet10Test(KafkaDotNet10TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
