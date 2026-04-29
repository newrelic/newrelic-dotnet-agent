// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

public abstract class LinuxKafkaTest<T> : NewRelicIntegrationTest<T> where T : KafkaTestFixtureBase
{
    private readonly T _fixture;

    protected LinuxKafkaTest(T fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);

                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_KAFKA_TOPIC", _fixture.TopicName);
            },
            exerciseApplication: () =>
            {
                _fixture.Delay(10); // wait for kafka and app to be ready
                _fixture.TestLogger.WriteLine("Starting exercise application");
                _fixture.ExerciseApplication();

                _fixture.GetBootstrapServer();

                _fixture.TestLogger.WriteLine("Waiting for metrics to be harvested");
                // Timing breakdown:
                //   librdkafka statistics.interval.ms = 5000 — callback fires every 5s per client.
                //   Agent MetricsHarvestCycle = 10s (set by ConfigureFasterMetricsHarvestCycle).
                //   Rate metrics (-rate) require at least TWO drains to form a delta.
                //   30s wait => ≥3 drains and ≥5-6 stats callbacks per long-lived client.
                _fixture.Delay(30);
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromSeconds(15));

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }

    /// <summary>
    /// Verifies the composite-handler path in KafkaBuilderWrapper: when a customer installs
    /// their own SetStatisticsHandler before Build(), BOTH the customer's handler AND our
    /// internal-metric-caching handler must fire on every librdkafka statistics interval.
    /// The long-lived custom-stats producer and consumer in the test app have customer
    /// handlers installed and have been alive since container startup, so their callback
    /// counters have been incrementing continuously.
    /// </summary>
    [Fact]
    public void CompositeStatisticsHandler_CustomerAndAgentHandlersBothFire()
    {
        var statusResult = _fixture.CustomStatisticsStatus;
        Assert.False(string.IsNullOrEmpty(statusResult),
            "Fixture did not capture /customstatisticsstatus response — exercise may have failed to reach the status endpoint.");

        var statusProducerCount = ExtractNamedCount(statusResult, "Producer callbacks");
        var statusConsumerCount = ExtractNamedCount(statusResult, "Consumer callbacks");

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var internalMetrics = metrics.Where(m => m.MetricSpec.Name.Contains("MessageBroker/Kafka/Internal/")).ToList();
        var producerInternal = internalMetrics.Where(m => m.MetricSpec.Name.Contains("/producer-metrics/")).ToList();
        var consumerInternal = internalMetrics.Where(m => m.MetricSpec.Name.Contains("/consumer-metrics/")).ToList();

        NrAssert.Multiple(
            // Customer handlers fired — proves both callers in the Delegate.Combine composite
            // are invoked by librdkafka.
            () => Assert.True(statusProducerCount > 0,
                $"Customer producer statistics handler should have been invoked at least once; got {statusProducerCount}. Status: {statusResult}"),
            () => Assert.True(statusConsumerCount > 0,
                $"Customer consumer statistics handler should have been invoked at least once; got {statusConsumerCount}. Status: {statusResult}"),

            // Our internal-metric handler is the OTHER caller in the composite. If the customer
            // handler had replaced ours (composition broken), these would be empty — the drain
            // would have no cached JSON to parse.
            () => Assert.NotEmpty(producerInternal),
            () => Assert.NotEmpty(consumerInternal)
        );
    }

    /// <summary>
    /// Verifies the agent emits the expected Kafka metrics, including all the metric paths
    /// the APM Kafka UI queries via its MetricFormat templates. Also covers span + transaction
    /// behavior, rate derivation on the producer side, and the GroupCoordinator broker labelling.
    /// Independent of customer statistics handlers (the custom-stats clients' metrics happen to
    /// be part of the set but no assertion here depends on their callbacks being invoked).
    /// </summary>
    [Fact]
    public void KafkaMetrics_EmitsExpectedMetricsAndUIPaths()
    {
        var topicName = _fixture.TopicName;
        var bootstrapServer = _fixture.BootstrapServer;

        var messageBrokerProduce = "MessageBroker/Kafka/Topic/Produce/Named/" + topicName;
        var messageBrokerProduceSerializationKey = messageBrokerProduce + "/Serialization/Key";
        var messageBrokerProduceSerializationValue = messageBrokerProduce + "/Serialization/Value";

        var messageBrokerConsume = "MessageBroker/Kafka/Topic/Consume/Named/" + topicName;

        var consumeWithTimeoutTransactionName = @"OtherTransaction/Custom/KafkaTestApp.Consumer/ConsumeOneWithTimeoutAsync";
        var consumeWithCancellationTransactionName = @"OtherTransaction/Custom/KafkaTestApp.Consumer/ConsumeOneWithCancellationTokenAsync";
        var produceWebTransactionName = @"WebTransaction/MVC/Kafka/Produce";
        var produceWithExistingHeadersWebTransactionName = @"WebTransaction/MVC/Kafka/ProduceAsyncWithExistingHeaders";
        var produceWithCustomStatisticsWebTransactionName = @"WebTransaction/MVC/Kafka/ProduceWithCustomStatistics";

        var messageBrokerNode = $"MessageBroker/Kafka/Nodes/{bootstrapServer}";
        var messageBrokerNodeProduceTopic = $"MessageBroker/Kafka/Nodes/{bootstrapServer}/Produce/{topicName}";
        var messageBrokerNodeConsumeTopic = $"MessageBroker/Kafka/Nodes/{bootstrapServer}/Consume/{topicName}";

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var spans = _fixture.AgentLog.GetSpanEvents();
        var produceSpan = spans.FirstOrDefault(s => s.IntrinsicAttributes["name"].Equals(messageBrokerProduce));
        var consumeWithTimeoutTxnSpan = spans.FirstOrDefault(s => s.IntrinsicAttributes["name"].Equals(consumeWithTimeoutTransactionName));
        var consumeWithCancellationTxnSpan = spans.FirstOrDefault(s => s.IntrinsicAttributes["name"].Equals(consumeWithCancellationTransactionName));

        var internalMetrics = metrics.Where(m => m.MetricSpec.Name.Contains("MessageBroker/Kafka/Internal/")).ToList();

        // Cumulative counters — librdkafka "int" type. Reported as raw values and paired with a
        // derived -rate metric computed from delta / librdkafka ts_delta_seconds.
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
        // Reliable for both producer and consumer clients because all four are long-lived:
        //   - Main producer + custom-stats producer both constructed at app startup
        //   - Work consumer + custom-stats consumer both constructed in Consumer.StartAsync
        // So stats callbacks accumulate across ≥5 drain cycles during the 30s metric wait,
        // which is well above the 2-drain minimum the rate machinery needs.
        var producerRequestRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("/request-rate")).ToList();
        var producerOutgoingByteRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("/outgoing-byte-rate")).ToList();
        var producerIncomingByteRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("producer-metrics") && m.MetricSpec.Name.EndsWith("/incoming-byte-rate")).ToList();
        var consumerBytesConsumedRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("consumer-fetch-manager-metrics") && m.MetricSpec.Name.EndsWith("/bytes-consumed-rate")).ToList();
        var consumerRecordsConsumedRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("consumer-fetch-manager-metrics") && m.MetricSpec.Name.EndsWith("/records-consumed-rate")).ToList();
        var allRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("-rate")).ToList();

        // Protocol request-type rates derived from librdkafka broker "req" counters.
        // heartbeat-rate + commit-rate come from the GroupCoordinator broker (source=logical).
        // fetch-rate from the data broker; produce-rate from the producer.
        var heartbeatRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("/heartbeat-rate")).ToList();
        var commitRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("/commit-rate")).ToList();
        var fetchRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("/fetch-rate")).ToList();
        var produceRateMetrics = internalMetrics.Where(m => m.MetricSpec.Name.EndsWith("/produce-rate")).ToList();

        // GroupCoordinator should appear as a "coordinator" node label, not "seed"
        var coordinatorNodeMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("/node/coordinator/")).ToList();

        // Regression guard: the custom group name consumer-topic-metrics was renamed to
        // consumer-fetch-manager-metrics on 2026-04-27 to match the APM Kafka UI queries.
        // If any metric comes back with the old name, the UI's Topics table will stop receiving data.
        var oldConsumerTopicGroupMetrics = internalMetrics.Where(m => m.MetricSpec.Name.Contains("/consumer-topic-metrics/")).ToList();

        // UI-compatibility paths — the exact MetricFormat templates the APM Kafka UI queries.
        // Source: source.datanerd.us/app-experience/shared-component-named-queries/signals/ext-service/
        // kafka-{consumer,producer}.yml + APM/apm-agent-nerdlets common/utils/queries.js and
        // common/components/kafka-topic-table/KafkaTopicTable.tsx. Regex-anchored to reject loose
        // substring matches.
        // Consumer-tab charts
        var uiConsumerIncomingByteRate = MatchExact(internalMetrics, "consumer-metrics/client/[^/]+/incoming-byte-rate");
        var uiConsumerRequestRate = MatchExact(internalMetrics, "consumer-metrics/client/[^/]+/request-rate");
        var uiConsumerRequestCounter = MatchExact(internalMetrics, "consumer-metrics/client/[^/]+/request-counter");
        var uiConsumerFetchBytesConsumedRate = MatchExact(internalMetrics, "consumer-fetch-manager-metrics/client/[^/]+/bytes-consumed-rate");
        var uiConsumerFetchRecordsConsumedRate = MatchExact(internalMetrics, "consumer-fetch-manager-metrics/client/[^/]+/records-consumed-rate");
        var uiConsumerCoordinatorHeartbeatRate = MatchExact(internalMetrics, "consumer-coordinator-metrics/client/[^/]+/heartbeat-rate");
        var uiConsumerCoordinatorCommitRate = MatchExact(internalMetrics, "consumer-coordinator-metrics/client/[^/]+/commit-rate");
        var uiConsumerCoordinatorAssignedPartitions = MatchExact(internalMetrics, "consumer-coordinator-metrics/client/[^/]+/assigned-partitions");

        // Producer-tab charts
        var uiProducerOutgoingByteRate = MatchExact(internalMetrics, "producer-metrics/client/[^/]+/outgoing-byte-rate");
        var uiProducerRequestRate = MatchExact(internalMetrics, "producer-metrics/client/[^/]+/request-rate");
        var uiProducerRequestCounter = MatchExact(internalMetrics, "producer-metrics/client/[^/]+/request-counter");
        var uiProducerBatchSizeAvg = MatchExact(internalMetrics, "producer-metrics/client/[^/]+/batch-size-avg");

        // Summary-tab Topics table
        var uiProducerTopicByteRate = MatchExact(internalMetrics, "producer-topic-metrics/topic/[^/]+/client/[^/]+/byte-rate");
        var uiConsumerTopicBytesConsumedRate = MatchExact(internalMetrics, "consumer-fetch-manager-metrics/topic/[^/]+/client/[^/]+/bytes-consumed-rate");

        // Produce-side counts are deterministic:
        //   Main exercise: produce, produceasync, produceasyncwithexistingheaders,
        //                  produce, produceasync                          = 5 produces (normal producer)
        //   Custom-stats:  producewithcustomstatistics × 2                = 2 produces (custom-stats producer)
        //   Total produces = 7.
        //
        // Consume-side counts are non-deterministic: ConsumeOne*Async methods long-poll for 5s,
        // consuming whatever messages happen to be available. Metrics that multiply across consumes
        // (messageBrokerConsume, TraceContext/Accept, messageBrokerNode, messageBrokerNodeConsumeTopic)
        // are therefore asserted null.
        //
        // ASP.NET Core MVC strips the trailing "Async" suffix from action names, so
        // Produce+ProduceAsync share WebTransaction/MVC/Kafka/Produce (4 calls). The custom-stats
        // produce lives under its own transaction name (ProduceWithCustomStatistics, 2 calls),
        // and ProduceAsyncWithExistingHeaders is its own (1 call).
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = produceWebTransactionName, CallCountAllHarvests = 4 },
            new() { metricName = produceWithExistingHeadersWebTransactionName, CallCountAllHarvests = 1 },
            new() { metricName = produceWithCustomStatisticsWebTransactionName, CallCountAllHarvests = 2 },
            new() { metricName = messageBrokerProduce, CallCountAllHarvests = 7 },
            new() { metricName = messageBrokerProduce, metricScope = produceWebTransactionName, CallCountAllHarvests = 4 },
            new() { metricName = messageBrokerProduce, metricScope = produceWithExistingHeadersWebTransactionName, CallCountAllHarvests = 1 },
            new() { metricName = messageBrokerProduce, metricScope = produceWithCustomStatisticsWebTransactionName, CallCountAllHarvests = 2 },
            new() { metricName = messageBrokerProduceSerializationKey, CallCountAllHarvests = 7 },
            new() { metricName = messageBrokerProduceSerializationKey, metricScope = produceWebTransactionName, CallCountAllHarvests = 4 },
            new() { metricName = messageBrokerProduceSerializationValue, CallCountAllHarvests = 7 },
            new() { metricName = messageBrokerProduceSerializationValue, metricScope = produceWebTransactionName, CallCountAllHarvests = 4 },

            new() { metricName = consumeWithTimeoutTransactionName, CallCountAllHarvests = 3 },
            new() { metricName = consumeWithCancellationTransactionName, CallCountAllHarvests = 2 },

            // One DT header inject per produce — deterministic like the produce count.
            new() { metricName = "Supportability/TraceContext/Create/Success", CallCountAllHarvests = 7 },

            // One Kafka node metric per produce. Consume-side contributions are non-deterministic,
            // so messageBrokerNode (which sums produce+consume) stays null.
            new() { metricName = messageBrokerNodeProduceTopic, CallCountAllHarvests = 7 },

            // Non-deterministic — depend on how many messages the long-polling consumers see.
            new() { metricName = messageBrokerConsume, CallCountAllHarvests = null },
            new() { metricName = messageBrokerConsume, metricScope = consumeWithTimeoutTransactionName, CallCountAllHarvests = null },
            new() { metricName = "Supportability/TraceContext/Accept/Success", CallCountAllHarvests = null },
            new() { metricName = messageBrokerNode, CallCountAllHarvests = null },
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

            // Rate metrics: the drain derives -rate from two consecutive observations of a
            // -total/-counter/-count metric with delta > 0. All four Kafka clients in the test
            // app are long-lived (main producer, custom-stats producer, work consumer, custom-stats
            // consumer), so stats callbacks accumulate across multiple drain cycles and rate
            // derivation is reliable for both producer-side and consumer-side metrics.
            () => Assert.True(producerRequestRateMetrics.Any(), "Producer request-rate metrics should exist (derived from request-counter)"),
            () => Assert.True(producerOutgoingByteRateMetrics.Any(), "Producer outgoing-byte-rate metrics should exist (derived from outgoing-byte-total)"),
            () => Assert.True(producerIncomingByteRateMetrics.Any(), "Producer incoming-byte-rate metrics should exist (derived from incoming-byte-total)"),
            () => Assert.True(consumerBytesConsumedRateMetrics.Any(), "Consumer bytes-consumed-rate metrics should exist (derived from bytes-consumed-total)"),
            () => Assert.True(consumerRecordsConsumedRateMetrics.Any(), "Consumer records-consumed-rate metrics should exist (derived from records-consumed-total)"),
            () => Assert.All(allRateMetrics,
                m => Assert.True(m.Values.Total > 0, $"Rate metric {m.MetricSpec.Name} should have a positive value")),

            // Protocol-level rates from broker "req" field.
            () => Assert.True(heartbeatRateMetrics.Any(), "Consumer heartbeat-rate metrics should exist"),
            () => Assert.True(commitRateMetrics.Any(), "Consumer commit-rate metrics should exist"),
            () => Assert.True(fetchRateMetrics.Any(), "Consumer fetch-rate metrics should exist"),
            () => Assert.True(produceRateMetrics.Any(), "Producer produce-rate metrics should exist"),

            // The GroupCoordinator logical broker must be labelled "coordinator", not conflated with "seed"
            () => Assert.True(coordinatorNodeMetrics.Any(),
                "GroupCoordinator broker should be exposed under node/coordinator (not collapsed into node/seed)"),

            // Regression guard: old custom group name must NEVER reappear.
            () => Assert.Empty(oldConsumerTopicGroupMetrics),

            // UI-compatibility paths (verified against APM Kafka UI queries on 2026-04-27).
            // Each line says "the UI expects exactly this path pattern; we must emit it."
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
            () => Assert.InRange((long)consumeWithTimeoutTxnSpan.UserAttributes["kafka.consume.byteCount"], 450, 500),
            () => Assert.True(consumeWithTimeoutTxnSpan.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(consumeWithTimeoutTxnSpan.IntrinsicAttributes.ContainsKey("parentId")),

            // Cancellation-token span: asserted only when present. Whether it's sampled depends on
            // priority and whether at least one message was consumed within the 5-second window.
            () =>
            {
                if (consumeWithCancellationTxnSpan == null) return;
                Assert.True(consumeWithCancellationTxnSpan.UserAttributes.ContainsKey("kafka.consume.byteCount"));
                Assert.InRange((long)consumeWithCancellationTxnSpan.UserAttributes["kafka.consume.byteCount"], 450, 500);
                Assert.True(consumeWithCancellationTxnSpan.IntrinsicAttributes.ContainsKey("traceId"));
                Assert.True(consumeWithCancellationTxnSpan.IntrinsicAttributes.ContainsKey("parentId"));
            }
        );
    }

    /// <summary>
    /// Extracts a named count from a response string of the form "... {label}: N ...".
    /// Returns 0 if no count is found for that label.
    /// </summary>
    private static int ExtractNamedCount(string response, string label)
    {
        if (string.IsNullOrEmpty(response)) return 0;
        var match = Regex.Match(response, Regex.Escape(label) + @":\s*(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
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
