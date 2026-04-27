// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

        // Cumulative counters — librdkafka "int" type. Reported as raw values and also paired with
        // a derived -rate metric computed from delta / librdkafka ts_delta_seconds.
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

        // Rate metrics — derived from cumulative counter deltas / librdkafka ts interval.
        // Present from the second harvest onward (first drain has no previous ts to divide by).
        // The 30s test wait with 10s harvest cycle guarantees at least 2 drains.
        var producerRequestRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("/request-rate")).ToList();
        var producerOutgoingByteRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("/outgoing-byte-rate")).ToList();
        var producerIncomingByteRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("/incoming-byte-rate")).ToList();
        var consumerBytesConsumedRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("consumer-fetch-manager-metrics") && m.MetricSpec.Name.EndsWith("/bytes-consumed-rate")).ToList();
        var consumerRecordsConsumedRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("consumer-fetch-manager-metrics") && m.MetricSpec.Name.EndsWith("/records-consumed-rate")).ToList();
        var allRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("-rate")).ToList();

        // Protocol request-type rates derived from librdkafka broker "req" counters.
        // heartbeat-rate + commit-rate come from the GroupCoordinator broker (exposed via the
        // logical broker with source=logical). fetch-rate comes from the data broker.
        var heartbeatRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("/heartbeat-rate")).ToList();
        var commitRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("/commit-rate")).ToList();
        var fetchRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("/fetch-rate")).ToList();
        var produceRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("/produce-rate")).ToList();
        // GroupCoordinator should appear as a "coordinator" node label, not "seed"
        var coordinatorNodeMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("/node/coordinator/")).ToList();

        // Regression guard: the old custom group name consumer-topic-metrics was renamed to
        // consumer-fetch-manager-metrics on 2026-04-27 to match the APM Kafka UI's
        // WITH METRIC_FORMAT queries. If any metric comes back with the old name, the rename
        // regressed and the UI's Topics table will stop receiving data.
        var oldConsumerTopicGroupMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("/consumer-topic-metrics/")).ToList();

        // UI-compatibility paths. These are the exact MetricFormat templates the APM Kafka UI
        // queries (source: source.datanerd.us/app-experience/shared-component-named-queries/
        // signals/ext-service/kafka-{consumer,producer}.yml, plus APM/apm-agent-nerdlets
        // common/utils/queries.js and common/components/kafka-topic-table/KafkaTopicTable.tsx).
        // Regex-anchored so we hit the exact group + client/topic placement — not loose
        // substring matches that can pass for unrelated metric paths.
        var uiConsumerIncomingByteRate = MatchExact(internalMetrics, "consumer-metrics/client/[^/]+/incoming-byte-rate");
        var uiConsumerRequestRate = MatchExact(internalMetrics, "consumer-metrics/client/[^/]+/request-rate");
        var uiConsumerRequestCounter = MatchExact(internalMetrics, "consumer-metrics/client/[^/]+/request-counter");
        var uiConsumerFetchBytesConsumedRate = MatchExact(internalMetrics, "consumer-fetch-manager-metrics/client/[^/]+/bytes-consumed-rate");
        var uiConsumerFetchRecordsConsumedRate = MatchExact(internalMetrics, "consumer-fetch-manager-metrics/client/[^/]+/records-consumed-rate");
        var uiConsumerCoordinatorHeartbeatRate = MatchExact(internalMetrics, "consumer-coordinator-metrics/client/[^/]+/heartbeat-rate");
        var uiConsumerCoordinatorCommitRate = MatchExact(internalMetrics, "consumer-coordinator-metrics/client/[^/]+/commit-rate");
        var uiConsumerCoordinatorAssignedPartitions = MatchExact(internalMetrics, "consumer-coordinator-metrics/client/[^/]+/assigned-partitions");
        var uiProducerOutgoingByteRate = MatchExact(internalMetrics, "producer-metrics/client/[^/]+/outgoing-byte-rate");
        var uiProducerRequestRate = MatchExact(internalMetrics, "producer-metrics/client/[^/]+/request-rate");
        var uiProducerRequestCounter = MatchExact(internalMetrics, "producer-metrics/client/[^/]+/request-counter");
        var uiProducerBatchSizeAvg = MatchExact(internalMetrics, "producer-metrics/client/[^/]+/batch-size-avg");
        // Topic-level (UI Topics table on Summary tab):
        var uiProducerTopicByteRate = MatchExact(internalMetrics, "producer-topic-metrics/topic/[^/]+/client/[^/]+/byte-rate");
        var uiConsumerTopicBytesConsumedRate = MatchExact(internalMetrics, "consumer-fetch-manager-metrics/topic/[^/]+/client/[^/]+/bytes-consumed-rate");

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

            // Rate metrics: present after second harvest because first drain has no previous ts.
            // The rate machinery strips trailing -total/-counter/-count and appends -rate.
            () => Assert.True(producerRequestRateMetrics.Any(), "Producer request-rate metrics should exist after multiple harvests (derived from request-counter)"),
            () => Assert.True(producerOutgoingByteRateMetrics.Any(), "Producer outgoing-byte-rate metrics should exist (derived from outgoing-byte-total)"),
            () => Assert.True(producerIncomingByteRateMetrics.Any(), "Producer incoming-byte-rate metrics should exist (derived from incoming-byte-total)"),
            () => Assert.True(consumerBytesConsumedRateMetrics.Any(), "Consumer bytes-consumed-rate metrics should exist (derived from bytes-consumed-total)"),
            () => Assert.True(consumerRecordsConsumedRateMetrics.Any(), "Consumer records-consumed-rate metrics should exist (derived from records-consumed-total)"),
            // All rate metrics must carry a positive value — zero would mean division-by-zero or no real activity
            () => Assert.All(allRateMetrics,
                m => Assert.True(m.Values.Total > 0, $"Rate metric {m.MetricSpec.Name} should have a positive value")),

            // Protocol-level rates from broker "req" field — these are what Java exposes as "protocol-level rates"
            () => Assert.True(heartbeatRateMetrics.Any(), "Consumer heartbeat-rate metrics should exist"),
            () => Assert.True(commitRateMetrics.Any(), "Consumer commit-rate metrics should exist"),
            () => Assert.True(fetchRateMetrics.Any(), "Consumer fetch-rate metrics should exist"),
            () => Assert.True(produceRateMetrics.Any(), "Producer produce-rate metrics should exist"),

            // The GroupCoordinator logical broker must be labelled "coordinator", not conflated with "seed"
            () => Assert.True(coordinatorNodeMetrics.Any(),
                "GroupCoordinator broker should be exposed under node/coordinator (not collapsed into node/seed)"),

            // Regression guard: old custom group name must NEVER reappear. If it does, the APM UI's
            // Topics table on the Summary tab stops receiving data (it queries consumer-fetch-manager-metrics).
            () => Assert.Empty(oldConsumerTopicGroupMetrics),

            // UI-compatibility path existence (verified against APM Kafka UI queries on 2026-04-27).
            // Each of these lines says "the UI expects exactly this path pattern; we must emit it."
            // Consumer-tab charts:
            () => Assert.True(uiConsumerIncomingByteRate.Any(), "UI query: consumer-metrics/client/{clientId}/incoming-byte-rate"),
            () => Assert.True(uiConsumerRequestRate.Any(), "UI query: consumer-metrics/client/{clientId}/request-rate"),
            () => Assert.True(uiConsumerRequestCounter.Any(), "UI query: consumer-metrics/client/{clientId}/request-counter"),
            () => Assert.True(uiConsumerFetchBytesConsumedRate.Any(), "UI query: consumer-fetch-manager-metrics/client/{clientId}/bytes-consumed-rate"),
            () => Assert.True(uiConsumerFetchRecordsConsumedRate.Any(), "UI query: consumer-fetch-manager-metrics/client/{clientId}/records-consumed-rate"),
            () => Assert.True(uiConsumerCoordinatorHeartbeatRate.Any(), "UI query: consumer-coordinator-metrics/client/{clientId}/heartbeat-rate"),
            () => Assert.True(uiConsumerCoordinatorCommitRate.Any(), "UI query: consumer-coordinator-metrics/client/{clientId}/commit-rate"),
            () => Assert.True(uiConsumerCoordinatorAssignedPartitions.Any(), "UI query: consumer-coordinator-metrics/client/{clientId}/assigned-partitions"),
            // Producer-tab charts:
            () => Assert.True(uiProducerOutgoingByteRate.Any(), "UI query: producer-metrics/client/{clientId}/outgoing-byte-rate"),
            () => Assert.True(uiProducerRequestRate.Any(), "UI query: producer-metrics/client/{clientId}/request-rate"),
            () => Assert.True(uiProducerRequestCounter.Any(), "UI query: producer-metrics/client/{clientId}/request-counter"),
            () => Assert.True(uiProducerBatchSizeAvg.Any(), "UI query: producer-metrics/client/{clientId}/batch-size-avg"),
            // Summary-tab Topics table:
            () => Assert.True(uiProducerTopicByteRate.Any(), "UI query: producer-topic-metrics/topic/{t}/client/{ci}/byte-rate"),
            () => Assert.True(uiConsumerTopicBytesConsumedRate.Any(), "UI query: consumer-fetch-manager-metrics/topic/{t}/client/{ci}/bytes-consumed-rate"),

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

    /// <summary>
    /// Filters metrics to those whose name exactly matches MessageBroker/Kafka/Internal/{suffixPattern}
    /// where suffixPattern is a regex fragment. Anchored at start and end so it rejects loose
    /// substring matches (e.g. "consumer-metrics" would otherwise also match "consumer-fetch-manager-metrics").
    /// </summary>
    private static List<NewRelic.Agent.Tests.TestSerializationHelpers.Models.Metric> MatchExact(
        IEnumerable<NewRelic.Agent.Tests.TestSerializationHelpers.Models.Metric> metrics,
        string suffixPattern)
    {
        var fullPattern = @"^MessageBroker/Kafka/Internal/" + suffixPattern + "$";
        return metrics.Where(m => Regex.IsMatch(m.MetricSpec.Name, fullPattern)).ToList();
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
