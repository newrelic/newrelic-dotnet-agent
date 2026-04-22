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
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);

                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_KAFKA_TOPIC", _topicName);
            },
            exerciseApplication: () =>
            {
                _fixture.Delay(10); // wait for kafka and app to be ready
                _fixture.TestLogger.WriteLine("Starting exercise application");
                _fixture.ExerciseApplication();

                _bootstrapServer = _fixture.GetBootstrapServer();

                _fixture.TestLogger.WriteLine("Waiting for metrics to be harvested");
                // With 10s harvest cycle and 10s drain interval, wait for a few cycles
                // to ensure cumulative deltas and gauge metrics are collected
                _fixture.Delay(30);
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromSeconds(15));

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

        // Extract internal metrics for validation (client IDs are dynamic, so we use pattern matching)
        var internalMetrics = metrics.Where(m => m.MetricSpec.Name.Contains("MessageBroker/Kafka/Internal/")).ToList();

        // Cumulative counters — ever-increasing, reported as deltas by the drain task
        var producerRequestMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("request-counter")).ToList();
        var producerResponseMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("response-counter")).ToList();
        var consumerRequestMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("consumer-metrics") && m.MetricSpec.Name.EndsWith("request-counter")).ToList();
        var consumerResponseMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("consumer-metrics") && m.MetricSpec.Name.EndsWith("response-counter")).ToList();
        var producerOutgoingByteMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("/outgoing-byte-total")).ToList();

        // Gauge — point-in-time snapshot, reported as raw value
        var producerMetadataCacheMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("/metadata_cache_cnt")).ToList();
        var consumerMetadataCacheMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("consumer-metrics") && m.MetricSpec.Name.EndsWith("/metadata_cache_cnt")).ToList();

        // WindowAvg — per-interval average, reported as raw value
        var producerBatchSizeAvgMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("/batch-size-avg")).ToList();
        var brokerRequestLatencyAvgMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("node-metrics") && m.MetricSpec.Name.EndsWith("/request-latency-avg")).ToList();

        // Note: With long-lived consumers (15 seconds each), message consumption patterns have changed
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = produceWebTransactionName, CallCountAllHarvests = 4 }, // includes sync and async actions
            new() { metricName = produceWithExistingHeadersWebTransactionName, CallCountAllHarvests = 1 }, // produce with pre-existing DT headers
            new() { metricName = messageBrokerProduce, CallCountAllHarvests = null }, // variable: 4 normal + 1 existing headers + 2 custom-stats produces
            new() { metricName = messageBrokerProduce, metricScope = produceWebTransactionName, CallCountAllHarvests = 4 },
            new() { metricName = messageBrokerProduce, metricScope = produceWithExistingHeadersWebTransactionName, CallCountAllHarvests = 1 },
            new() { metricName = messageBrokerProduceSerializationKey, CallCountAllHarvests = null }, // variable: same as messageBrokerProduce
            new() { metricName = messageBrokerProduceSerializationKey, metricScope = produceWebTransactionName, CallCountAllHarvests = 4 },
            new() { metricName = messageBrokerProduceSerializationValue, CallCountAllHarvests = null }, // variable: same as messageBrokerProduce
            new() { metricName = messageBrokerProduceSerializationValue, metricScope = produceWebTransactionName, CallCountAllHarvests = 4 },

            new() { metricName = consumeWithTimeoutTransactionName, CallCountAllHarvests = 3 }, // 2 normal + 1 for existing headers
            new() { metricName = consumeWithCancellationTransactionName, CallCountAllHarvests = 2 },

            // Consume counts are variable — depends on consumer duration and message availability
            new() { metricName = messageBrokerConsume, CallCountAllHarvests = null },
            new() { metricName = messageBrokerConsume, metricScope = consumeWithTimeoutTransactionName, CallCountAllHarvests = null },
            new() { metricName = messageBrokerConsume, metricScope = consumeWithCancellationTransactionName, CallCountAllHarvests = 2 },

            // TraceContext metrics — variable due to custom-stats produces/consumes
            new() { metricName = "Supportability/TraceContext/Create/Success", CallCountAllHarvests = null },
            new() { metricName = "Supportability/TraceContext/Accept/Success", CallCountAllHarvests = null },

            // Node metrics: variable due to custom-stats producers
            new() { metricName = messageBrokerNode, CallCountAllHarvests = null },
            new() { metricName = messageBrokerNodeProduceTopic, CallCountAllHarvests = null }, // variable: normal + existing headers + custom stats
            new() { metricName = messageBrokerNodeConsumeTopic, CallCountAllHarvests = null },
        };

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),

            // Cumulative counters: request-counter, response-counter, txmsgs
            () => Assert.True(producerRequestMetrics.Any(), "Producer request-counter metrics should exist"),
            () => Assert.True(producerResponseMetrics.Any(), "Producer response-counter metrics should exist"),
            () => Assert.True(consumerRequestMetrics.Any(), "Consumer request-counter metrics should exist"),
            () => Assert.True(consumerResponseMetrics.Any(), "Consumer response-counter metrics should exist"),
            () => Assert.True(producerOutgoingByteMetrics.Any(), "Producer outgoing-byte-total metrics should exist"),

            // Gauge: metadata_cache_cnt (both producer and consumer should have metadata cached)
            () => Assert.True(producerMetadataCacheMetrics.Any(), "Producer metadata_cache_cnt metrics should exist"),
            () => Assert.True(consumerMetadataCacheMetrics.Any(), "Consumer metadata_cache_cnt metrics should exist"),

            // WindowAvg: batch-size-avg (producer), request-latency-avg (broker node)
            () => Assert.True(producerBatchSizeAvgMetrics.Any(), "Producer batch-size-avg metrics should exist"),
            () => Assert.True(brokerRequestLatencyAvgMetrics.Any(), "Broker request-latency-avg metrics should exist"),

            // All internal metrics should have non-zero call count (gauge wire format uses count=1)
            () => Assert.All(internalMetrics,
                m => Assert.True(m.Values.CallCount > 0, $"Internal metric {m.MetricSpec.Name} should have non-zero call count")),

            // Producer span assertions (always present)
            () => Assert.True(produceSpan.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(produceSpan.IntrinsicAttributes.ContainsKey("parentId")),

            // ConsumeWithTimeout span assertions (should exist since it consumes messages)
            () => Assert.NotNull(consumeWithTimeoutTxnSpan),
            () => Assert.True(consumeWithTimeoutTxnSpan.UserAttributes.ContainsKey("kafka.consume.byteCount")),
            () => Assert.InRange((long)consumeWithTimeoutTxnSpan.UserAttributes["kafka.consume.byteCount"], 450, 500), // includes headers; upper bound accommodates existing-headers message
            () => Assert.True(consumeWithTimeoutTxnSpan.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(consumeWithTimeoutTxnSpan.IntrinsicAttributes.ContainsKey("parentId")),
            () => Assert.NotNull(consumeWithCancellationTxnSpan),
            () => Assert.True(consumeWithCancellationTxnSpan.UserAttributes.ContainsKey("kafka.consume.byteCount")),
            () => Assert.InRange((long)consumeWithCancellationTxnSpan.UserAttributes["kafka.consume.byteCount"], 450, 500), // includes headers; upper bound accommodates existing-headers message
            () => Assert.True(consumeWithCancellationTxnSpan.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(consumeWithCancellationTxnSpan.IntrinsicAttributes.ContainsKey("parentId"))
        );

        ValidateInternalMetrics();
    }

    private void ValidateInternalMetrics()
    {
        // Get results from the custom statistics testing that ran during ExerciseApplication
        var customStatisticsResults = _fixture.CustomStatisticsResults;
        Assert.NotNull(customStatisticsResults);

        var (produceResult, consumeResult, statusResult) = customStatisticsResults.Value;

        // Get metrics after all application operations
        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var internalMetrics = metrics.Where(m => m.MetricSpec.Name.Contains("MessageBroker/Kafka/Internal/")).ToList();

        // Find producer and consumer internal metrics (should still exist even with customer handlers)
        var producerRequestMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("request-counter")).ToList();
        var producerResponseMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("response-counter")).ToList();
        var consumerRequestMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("consumer-metrics") && m.MetricSpec.Name.EndsWith("request-counter")).ToList();
        var consumerResponseMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("consumer-metrics") && m.MetricSpec.Name.EndsWith("response-counter")).ToList();

        NrAssert.Multiple(
            // Verify that API responses contain evidence of customer handler activity
            () => Assert.Contains("callback count", produceResult, StringComparison.InvariantCultureIgnoreCase),
            () => Assert.Contains("callback count", consumeResult, StringComparison.InvariantCultureIgnoreCase),
            () => Assert.Contains("Producer callbacks:", statusResult, StringComparison.InvariantCultureIgnoreCase),
            () => Assert.Contains("Consumer callbacks:", statusResult, StringComparison.InvariantCultureIgnoreCase),

            // Verify our internal metrics are STILL being collected despite customer handlers
            () => Assert.True(producerRequestMetrics.Any(), "Producer request-counter internal metrics should still exist with custom handlers"),
            () => Assert.True(producerResponseMetrics.Any(), "Producer response-counter internal metrics should still exist with custom handlers"),
            () => Assert.True(consumerRequestMetrics.Any() || consumerResponseMetrics.Any(),
                "At least some consumer internal metrics should exist with custom handlers (consumer may not get messages but should still generate some statistics)"),

            // Verify internal metric names still follow expected pattern (proving our metrics coexist with customer's)
            () => Assert.All(producerRequestMetrics.Concat(producerResponseMetrics),
                m => Assert.Contains("MessageBroker/Kafka/Internal/", m.MetricSpec.Name)),
            () => Assert.All(consumerRequestMetrics.Concat(consumerResponseMetrics),
                m => Assert.Contains("MessageBroker/Kafka/Internal/", m.MetricSpec.Name)),

            // Verify internal metrics have reasonable values (indicating our composite handlers are working)
            () => Assert.All(producerRequestMetrics.Concat(producerResponseMetrics).Where(m => m.Values.CallCount > 0),
                m => Assert.True(m.Values.CallCount > 0, $"Internal metric {m.MetricSpec.Name} should have non-zero count when composite handlers are active"))
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
