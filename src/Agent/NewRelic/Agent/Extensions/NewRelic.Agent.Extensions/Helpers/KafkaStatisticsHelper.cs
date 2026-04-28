// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Extensions.Helpers;

/// <summary>
/// Classifies Kafka metrics by their librdkafka statistics type.
/// Determines whether delta computation is needed at drain time.
/// </summary>
public enum KafkaMetricType
{
    /// <summary>Ever-increasing counter from librdkafka ("int" type). Needs delta computation.</summary>
    Cumulative,

    /// <summary>Point-in-time snapshot from librdkafka ("int gauge" type). Use raw value.</summary>
    Gauge,

    /// <summary>Window average from librdkafka (rtt.avg, batchsize.avg). Use raw value.</summary>
    WindowAvg
}

/// <summary>
/// A metric value paired with its type, used to determine how to report the metric.
/// Struct to avoid heap allocation per metric entry.
/// </summary>
public struct KafkaMetricValue
{
    public long Value;
    public KafkaMetricType MetricType;

    public KafkaMetricValue(long value, KafkaMetricType metricType)
    {
        Value = value;
        MetricType = metricType;
    }
}

/// <summary>
/// Helper for parsing and extracting metrics from Kafka statistics JSON.
/// This helper is in the Extensions project so it has access to Newtonsoft.Json.
/// </summary>
public static class KafkaStatisticsHelper
{
    #region JSON Model Classes

    /// <summary>
    /// Model for librdkafka statistics JSON schema.
    /// Based on the official librdkafka STATISTICS.md documentation.
    /// </summary>
    public class KafkaStatistics
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>librdkafka's internal monotonic clock, in microseconds. Ever-increasing int.</summary>
        [JsonProperty("ts")]
        public long Ts { get; set; }

        [JsonProperty("tx")]
        public long Tx { get; set; }

        [JsonProperty("rx")]
        public long Rx { get; set; }

        [JsonProperty("txmsgs")]
        public long TxMsgs { get; set; }

        [JsonProperty("rxmsgs")]
        public long RxMsgs { get; set; }

        [JsonProperty("txmsg_bytes")]
        public long TxMsgBytes { get; set; }

        [JsonProperty("rxmsg_bytes")]
        public long RxMsgBytes { get; set; }

        [JsonProperty("metadata_cache_cnt")]
        public long MetadataCacheCnt { get; set; }

        [JsonProperty("msg_cnt")]
        public long MsgCnt { get; set; }

        [JsonProperty("msg_size")]
        public long MsgSize { get; set; }

        [JsonProperty("tx_bytes")]
        public long TxBytes { get; set; }

        [JsonProperty("rx_bytes")]
        public long RxBytes { get; set; }

        // Hierarchical metrics
        [JsonProperty("brokers")]
        public Dictionary<string, KafkaBrokerStats> Brokers { get; set; } = new();

        [JsonProperty("topics")]
        public Dictionary<string, KafkaTopicStats> Topics { get; set; } = new();

        [JsonProperty("cgrp")]
        public KafkaConsumerGroupStats ConsumerGroup { get; set; }

        [JsonProperty("eos")]
        public KafkaProducerIdempotentStats ProducerEos { get; set; }
    }

    /// <summary>
    /// Broker-level statistics from librdkafka
    /// </summary>
    public class KafkaBrokerStats
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("nodeid")]
        public int NodeId { get; set; }

        /// <summary>
        /// Broker source: "learned", "configured", "internal", or "logical".
        /// "logical" marks synthetic brokers like GroupCoordinator which share nodeid=-1 with seed brokers.
        /// </summary>
        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("tx")]
        public long Tx { get; set; }

        [JsonProperty("rx")]
        public long Rx { get; set; }

        [JsonProperty("txbytes")]
        public long TxBytes { get; set; }

        [JsonProperty("rxbytes")]
        public long RxBytes { get; set; }

        [JsonProperty("txerrs")]
        public long TxErrs { get; set; }

        [JsonProperty("rxerrs")]
        public long RxErrs { get; set; }

        [JsonProperty("connects")]
        public long Connects { get; set; }

        [JsonProperty("disconnects")]
        public long Disconnects { get; set; }

        [JsonProperty("rtt")]
        public KafkaWindowStats RoundTripTime { get; set; }

        /// <summary>
        /// Per-API-type request counters. Keys are Kafka protocol API names ("Heartbeat", "Fetch",
        /// "OffsetCommit", "Produce", etc). librdkafka also emits "Unknown-NN?" entries for API IDs
        /// it does not recognize — these are filtered out at emit time.
        /// </summary>
        [JsonProperty("req")]
        public Dictionary<string, long> RequestCounts { get; set; } = new();
    }

    /// <summary>
    /// Topic-level statistics from librdkafka
    /// </summary>
    public class KafkaTopicStats
    {
        [JsonProperty("topic")]
        public string Topic { get; set; }

        [JsonProperty("metadata_age")]
        public long MetadataAge { get; set; }

        [JsonProperty("batchsize")]
        public KafkaWindowStats BatchSize { get; set; }

        [JsonProperty("batchcnt")]
        public KafkaWindowStats BatchCount { get; set; }

        [JsonProperty("partitions")]
        public Dictionary<string, KafkaPartitionStats> Partitions { get; set; } = new();
    }

    /// <summary>
    /// Partition-level statistics from librdkafka
    /// </summary>
    public class KafkaPartitionStats
    {
        [JsonProperty("partition")]
        public int Partition { get; set; }

        [JsonProperty("broker")]
        public int Broker { get; set; }

        [JsonProperty("leader")]
        public int Leader { get; set; }

        [JsonProperty("txmsgs")]
        public long TxMsgs { get; set; }

        [JsonProperty("txbytes")]
        public long TxBytes { get; set; }

        [JsonProperty("rxmsgs")]
        public long RxMsgs { get; set; }

        [JsonProperty("rxbytes")]
        public long RxBytes { get; set; }

        [JsonProperty("consumer_lag")]
        public long ConsumerLag { get; set; }

        [JsonProperty("lo_offset")]
        public long LowWatermark { get; set; }

        [JsonProperty("hi_offset")]
        public long HighWatermark { get; set; }

        [JsonProperty("committed_offset")]
        public long CommittedOffset { get; set; }
    }

    /// <summary>
    /// Consumer group statistics from librdkafka
    /// </summary>
    public class KafkaConsumerGroupStats
    {
        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("rebalance_cnt")]
        public long RebalanceCount { get; set; }

        [JsonProperty("rebalance_age")]
        public long RebalanceAge { get; set; }

        [JsonProperty("assignment_size")]
        public int AssignmentSize { get; set; }

        [JsonProperty("rebalance_reason")]
        public string RebalanceReason { get; set; }
    }

    /// <summary>
    /// Producer idempotent/EOS statistics from librdkafka
    /// </summary>
    public class KafkaProducerIdempotentStats
    {
        [JsonProperty("idemp_state")]
        public string IdempotentState { get; set; }

        [JsonProperty("producer_id")]
        public long ProducerId { get; set; }

        [JsonProperty("producer_epoch")]
        public int ProducerEpoch { get; set; }

        [JsonProperty("epoch_cnt")]
        public long EpochCount { get; set; }
    }

    /// <summary>
    /// Window statistics (min, max, avg, sum, cnt) from librdkafka
    /// </summary>
    public class KafkaWindowStats
    {
        [JsonProperty("min")]
        public long Min { get; set; }

        [JsonProperty("max")]
        public long Max { get; set; }

        [JsonProperty("avg")]
        public long Avg { get; set; }

        [JsonProperty("sum")]
        public long Sum { get; set; }

        [JsonProperty("cnt")]
        public long Count { get; set; }
    }

    #endregion

    // Prefix computation is inlined per call rather than cached across calls. A prior static
    // ConcurrentDictionary cache keyed by (clientId, group) was unbounded and would retain
    // prefix strings for the lifetime of the process, growing for workloads that churn through
    // many short-lived Kafka clients with distinct client.id values. The Concat cost is
    // trivial at drain cadence (~20 calls per client per drain), so bounding by eliminating
    // the cache is the simplest correct fix. The caller pattern of storing the prefix in a
    // local and reusing it across multiple AddMetric calls preserves within-function reuse.
    private static string BuildPrefix(string clientId, string group)
    {
        return string.Concat("MessageBroker/", MessageBrokerVendorConstants.Kafka, "/Internal/", group, "/client/", clientId);
    }

    /// <summary>
    /// Maps librdkafka Kafka API names (as they appear in the broker "req" dictionary) to the
    /// metric-name suffixes we emit. Suffixes end in "-total" so the rate machinery strips it
    /// and produces heartbeat-rate, fetch-rate, etc. automatically. Unknown-NN? keys and API
    /// types we don't care about are implicitly filtered by being absent from this map.
    /// </summary>
    private static readonly Dictionary<string, string> _protocolRequestMetricSuffixes = new()
    {
        { "Heartbeat", "heartbeat-total" },
        { "Fetch", "fetch-total" },
        { "Produce", "produce-total" },
        { "OffsetCommit", "commit-total" },
        { "JoinGroup", "join-total" },
        { "SyncGroup", "sync-total" },
        { "LeaveGroup", "leave-total" },
        { "Metadata", "metadata-total" },
    };

    /// <summary>
    /// Parses Kafka statistics JSON into the deserialized model.
    /// Returns null if parsing fails or JSON is empty.
    /// </summary>
    public static KafkaStatistics ParseStatistics(string statisticsJson)
    {
        if (string.IsNullOrEmpty(statisticsJson))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<KafkaStatistics>(statisticsJson);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks whether the parsed statistics have the minimum required fields.
    /// </summary>
    public static bool IsValid(KafkaStatistics stats)
    {
        if (stats == null)
            return false;

        var clientId = !string.IsNullOrEmpty(stats.ClientId) ? stats.ClientId : stats.Name;
        return !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(stats.Type);
    }

    /// <summary>
    /// Returns the effective client ID (prefers client_id, falls back to name).
    /// </summary>
    public static string GetClientId(KafkaStatistics stats)
    {
        return !string.IsNullOrEmpty(stats.ClientId) ? stats.ClientId : stats.Name;
    }

    /// <summary>
    /// Test-only convenience: allocates a dictionary and populates it in one call. Not a
    /// supported public API — production code must use <see cref="PopulateMetricsDictionary"/>
    /// so the buffer can be reused across drains without reallocating. Kept at <c>public</c>
    /// visibility only because the test assembly is not a strong-name friend of this one;
    /// do not take a dependency on this method from production code or instrumentation wrappers.
    /// </summary>
    public static Dictionary<string, KafkaMetricValue> CreateMetricsDictionary(KafkaStatistics stats)
    {
        var metrics = new Dictionary<string, KafkaMetricValue>();
        PopulateMetricsDictionary(metrics, stats);
        return metrics;
    }

    /// <summary>
    /// Populates an existing dictionary with metrics from parsed statistics.
    /// Clears the dictionary first, reusing its internal storage.
    /// </summary>
    public static void PopulateMetricsDictionary(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats)
    {
        metrics.Clear();

        if (!IsValid(stats))
            return;

        var clientId = GetClientId(stats);
        var clientType = stats.Type;

        AddClientLevelMetrics(metrics, stats, clientType, clientId);

        if (clientType == "consumer" && stats.ConsumerGroup != null)
            AddConsumerMetrics(metrics, stats, clientId);

        if (clientType == "producer")
            AddProducerMetrics(metrics, stats, clientId);

        AddBrokerMetrics(metrics, stats, clientType, clientId);
        AddClientProtocolRequestMetrics(metrics, stats, clientType, clientId);
        AddTopicAndPartitionMetrics(metrics, stats, clientType, clientId);
    }

    private static void AddClientLevelMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientType, string clientId)
    {
        var basePrefix = BuildPrefix(clientId, clientType == "consumer" ? "consumer-metrics" : "producer-metrics");

        // Cumulative counters (librdkafka "int" type — ever-increasing)
        AddMetric(metrics, string.Concat(basePrefix, "/request-counter"), stats.Tx, KafkaMetricType.Cumulative);
        AddMetric(metrics, string.Concat(basePrefix, "/response-counter"), stats.Rx, KafkaMetricType.Cumulative);
        AddMetric(metrics, string.Concat(basePrefix, "/txmsgs"), stats.TxMsgs, KafkaMetricType.Cumulative);
        AddMetric(metrics, string.Concat(basePrefix, "/rxmsgs"), stats.RxMsgs, KafkaMetricType.Cumulative);
        AddMetric(metrics, string.Concat(basePrefix, "/txmsg_bytes"), stats.TxMsgBytes, KafkaMetricType.Cumulative);
        AddMetric(metrics, string.Concat(basePrefix, "/rxmsg_bytes"), stats.RxMsgBytes, KafkaMetricType.Cumulative);
        AddMetric(metrics, string.Concat(basePrefix, "/outgoing-byte-total"), stats.TxBytes, KafkaMetricType.Cumulative);
        AddMetric(metrics, string.Concat(basePrefix, "/incoming-byte-total"), stats.RxBytes, KafkaMetricType.Cumulative);

        // Gauges (librdkafka "int gauge" type — point-in-time snapshot)
        AddMetric(metrics, string.Concat(basePrefix, "/metadata_cache_cnt"), stats.MetadataCacheCnt, KafkaMetricType.Gauge);
        AddMetric(metrics, string.Concat(basePrefix, "/record-queue-time-avg"), stats.MsgCnt, KafkaMetricType.Gauge);
        if (stats.MsgCnt > 0)
            AddMetric(metrics, string.Concat(basePrefix, "/record-size-avg"), stats.MsgSize / stats.MsgCnt, KafkaMetricType.Gauge);
    }

    private static void AddConsumerMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientId)
    {
        var cgrp = stats.ConsumerGroup;
        var coordinatorPrefix = BuildPrefix(clientId, "consumer-coordinator-metrics");
        AddMetric(metrics, string.Concat(coordinatorPrefix, "/rebalance-total"), cgrp.RebalanceCount, KafkaMetricType.Cumulative);
        AddMetric(metrics, string.Concat(coordinatorPrefix, "/rebalance-latency-avg"), cgrp.RebalanceAge, KafkaMetricType.Gauge);
        AddMetric(metrics, string.Concat(coordinatorPrefix, "/assigned-partitions"), cgrp.AssignmentSize, KafkaMetricType.Gauge);

        // Compute total consumer lag across all partitions
        long totalConsumerLag = 0;
        if (stats.Topics != null)
        {
            foreach (var topic in stats.Topics.Values)
            {
                if (topic.Partitions == null) continue;
                foreach (var partition in topic.Partitions.Values)
                    totalConsumerLag += partition.ConsumerLag;
            }
        }

        var fetchPrefix = BuildPrefix(clientId, "consumer-fetch-manager-metrics");
        AddMetric(metrics, string.Concat(fetchPrefix, "/records-consumed-total"), stats.RxMsgs, KafkaMetricType.Cumulative);
        AddMetric(metrics, string.Concat(fetchPrefix, "/bytes-consumed-total"), stats.RxMsgBytes, KafkaMetricType.Cumulative);
        if (totalConsumerLag > 0 && cgrp.AssignmentSize > 0)
            AddMetric(metrics, string.Concat(fetchPrefix, "/records-lag-avg"), totalConsumerLag / cgrp.AssignmentSize, KafkaMetricType.Gauge);
        AddMetric(metrics, string.Concat(fetchPrefix, "/records-lag-max"), totalConsumerLag, KafkaMetricType.Gauge);
    }

    private static void AddProducerMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientId)
    {
        var producerPrefix = BuildPrefix(clientId, "producer-metrics");

        // Compute batch averages across topics with running sums (no LINQ/List allocation)
        long batchSizeSum = 0, batchSizeCount = 0;
        if (stats.Topics != null)
        {
            foreach (var topic in stats.Topics.Values)
            {
                if (topic.BatchSize != null)
                {
                    batchSizeSum += topic.BatchSize.Avg;
                    batchSizeCount++;
                }
            }
        }

        if (batchSizeCount > 0)
        {
            var batchSizeAvg = batchSizeSum / batchSizeCount;
            AddMetric(metrics, string.Concat(producerPrefix, "/batch-size-avg"), batchSizeAvg, KafkaMetricType.WindowAvg);
            if (batchSizeAvg > 0)
                AddMetric(metrics, string.Concat(producerPrefix, "/records-per-request-avg"), stats.TxMsgs / batchSizeAvg, KafkaMetricType.WindowAvg);
        }
        AddMetric(metrics, string.Concat(producerPrefix, "/record-send-total"), stats.TxMsgs, KafkaMetricType.Cumulative);

        // Idempotent producer metrics
        if (stats.ProducerEos != null && !string.IsNullOrEmpty(stats.ProducerEos.IdempotentState))
        {
            AddMetric(metrics, string.Concat(producerPrefix, "/producer-id-changes"), stats.ProducerEos.EpochCount, KafkaMetricType.Cumulative);
        }
    }

    private static void AddBrokerMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientType, string clientId)
    {
        if (stats.Brokers == null) return;

        foreach (var broker in stats.Brokers.Values)
        {
            var normalizedNodeId = NormalizeNodeId(broker);
            var nodePrefix = string.Concat("MessageBroker/", MessageBrokerVendorConstants.Kafka, "/Internal/", clientType, "-node-metrics/node/", normalizedNodeId, "/client/", clientId);

            AddMetric(metrics, string.Concat(nodePrefix, "/request-total"), broker.Tx, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(nodePrefix, "/response-total"), broker.Rx, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(nodePrefix, "/outgoing-byte-total"), broker.TxBytes, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(nodePrefix, "/incoming-byte-total"), broker.RxBytes, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(nodePrefix, "/request-latency-avg"), broker.RoundTripTime?.Avg ?? 0, KafkaMetricType.WindowAvg);
            AddMetric(metrics, string.Concat(nodePrefix, "/connection-count"), broker.Connects, KafkaMetricType.Cumulative);

            // Per-broker Kafka protocol request-type counters. The rate machinery in the drain loop
            // strips "-total" and produces heartbeat-rate, fetch-rate, etc. as a byproduct.
            if (broker.RequestCounts != null)
            {
                foreach (var kvp in _protocolRequestMetricSuffixes)
                {
                    if (broker.RequestCounts.TryGetValue(kvp.Key, out var count))
                        AddMetric(metrics, string.Concat(nodePrefix, "/", kvp.Value), count, KafkaMetricType.Cumulative);
                }
            }
        }
    }

    /// <summary>
    /// Emits client-level aggregated protocol request metrics that mirror the Java agent's naming:
    ///   consumer-coordinator-metrics/heartbeat-total + heartbeat-rate, commit-total + commit-rate,
    ///   join-total + join-rate, sync-total + sync-rate, leave-total + leave-rate
    ///   consumer-fetch-manager-metrics/fetch-total + fetch-rate
    ///   producer-metrics/produce-total + produce-rate, metadata-total + metadata-rate
    /// Sums the per-broker req counts from librdkafka's statistics JSON. Heartbeats/commits/joins/syncs
    /// only accumulate on the GroupCoordinator broker; fetches/produces only on data brokers.
    /// </summary>
    private static void AddClientProtocolRequestMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientType, string clientId)
    {
        if (stats.Brokers == null || stats.Brokers.Count == 0) return;

        long totalHeartbeats = 0, totalCommits = 0, totalJoins = 0, totalSyncs = 0, totalLeaves = 0;
        long totalFetches = 0, totalProduces = 0, totalMetadata = 0;

        foreach (var broker in stats.Brokers.Values)
        {
            if (broker.RequestCounts == null) continue;

            long v;
            if (broker.RequestCounts.TryGetValue("Heartbeat", out v)) totalHeartbeats += v;
            if (broker.RequestCounts.TryGetValue("OffsetCommit", out v)) totalCommits += v;
            if (broker.RequestCounts.TryGetValue("JoinGroup", out v)) totalJoins += v;
            if (broker.RequestCounts.TryGetValue("SyncGroup", out v)) totalSyncs += v;
            if (broker.RequestCounts.TryGetValue("LeaveGroup", out v)) totalLeaves += v;
            if (broker.RequestCounts.TryGetValue("Fetch", out v)) totalFetches += v;
            if (broker.RequestCounts.TryGetValue("Produce", out v)) totalProduces += v;
            if (broker.RequestCounts.TryGetValue("Metadata", out v)) totalMetadata += v;
        }

        if (clientType == "consumer")
        {
            var coordPrefix = BuildPrefix(clientId, "consumer-coordinator-metrics");
            var fetchPrefix = BuildPrefix(clientId, "consumer-fetch-manager-metrics");

            AddMetric(metrics, string.Concat(coordPrefix, "/heartbeat-total"), totalHeartbeats, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(coordPrefix, "/commit-total"), totalCommits, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(coordPrefix, "/join-total"), totalJoins, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(coordPrefix, "/sync-total"), totalSyncs, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(coordPrefix, "/leave-total"), totalLeaves, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(fetchPrefix, "/fetch-total"), totalFetches, KafkaMetricType.Cumulative);
        }
        else if (clientType == "producer")
        {
            var producerPrefix = BuildPrefix(clientId, "producer-metrics");
            AddMetric(metrics, string.Concat(producerPrefix, "/produce-total"), totalProduces, KafkaMetricType.Cumulative);
            AddMetric(metrics, string.Concat(producerPrefix, "/metadata-total"), totalMetadata, KafkaMetricType.Cumulative);
        }
    }

    /// <summary>
    /// Single-pass topic and partition metric generation. Avoids iterating partitions twice.
    /// </summary>
    private static void AddTopicAndPartitionMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientType, string clientId)
    {
        if (stats.Topics == null) return;

        foreach (var topic in stats.Topics.Values)
        {
            // Consumer and producer use different Kafka-native topic-level groups:
            //   consumer: consumer-fetch-manager-metrics  (per Kafka FetchMetricsRegistry.java)
            //   producer: producer-topic-metrics          (per Kafka SenderMetricsRegistry.TOPIC_METRIC_GROUP_NAME)
            // Verified against kafka-consumer.yml / KafkaTopicTable.tsx in the APM UI repo at
            // source.datanerd.us/APM/apm-agent-nerdlets — the UI queries these exact group names
            // via WITH METRIC_FORMAT 'MessageBroker/Kafka/Internal/{group}/topic/{t}/client/{c}/...'.
            var topicGroupName = clientType == "consumer" ? "consumer-fetch-manager-metrics" : "producer-topic-metrics";
            var topicPrefix = string.Concat("MessageBroker/", MessageBrokerVendorConstants.Kafka, "/Internal/", topicGroupName, "/topic/", topic.Topic, "/client/", clientId);
            var partitionBasePrefix = string.Concat("MessageBroker/", MessageBrokerVendorConstants.Kafka, "/Internal/", clientType, "-metrics/topic/", topic.Topic);

            // Aggregate partition stats and emit partition metrics in a single pass
            long totalTxMessages = 0, totalTxBytes = 0, totalRxMessages = 0, totalRxBytes = 0, totalConsumerLag = 0;
            var partitionCount = 0;

            if (topic.Partitions != null)
            {
                foreach (var partition in topic.Partitions.Values)
                {
                    partitionCount++;
                    totalTxMessages += partition.TxMsgs;
                    totalTxBytes += partition.TxBytes;
                    totalRxMessages += partition.RxMsgs;
                    totalRxBytes += partition.RxBytes;
                    totalConsumerLag += partition.ConsumerLag;

                    // Emit partition-level metrics inline
                    var partitionPrefix = string.Concat(partitionBasePrefix, "/partition/", partition.Partition.ToString(), "/client/", clientId);

                    if (clientType == "consumer")
                    {
                        AddMetric(metrics, string.Concat(partitionPrefix, "/records-consumed-total"), partition.RxMsgs, KafkaMetricType.Cumulative);
                        AddMetric(metrics, string.Concat(partitionPrefix, "/bytes-consumed-total"), partition.RxBytes, KafkaMetricType.Cumulative);
                        AddMetric(metrics, string.Concat(partitionPrefix, "/records-lag"), partition.ConsumerLag, KafkaMetricType.Gauge);
                        AddMetric(metrics, string.Concat(partitionPrefix, "/committed-offset"), partition.CommittedOffset, KafkaMetricType.Gauge);
                        AddMetric(metrics, string.Concat(partitionPrefix, "/position"), partition.HighWatermark, KafkaMetricType.Gauge);
                    }
                    else if (clientType == "producer")
                    {
                        AddMetric(metrics, string.Concat(partitionPrefix, "/record-send-total"), partition.TxMsgs, KafkaMetricType.Cumulative);
                        AddMetric(metrics, string.Concat(partitionPrefix, "/byte-total"), partition.TxBytes, KafkaMetricType.Cumulative);
                    }
                }
            }

            // Emit topic-level metrics using aggregated partition data.
            // byte-total must be a true monotonic counter (sum of cumulative partition txbytes),
            // not a derived value involving BatchSize.Avg — the latter is a window average that
            // fluctuates, which produced negative deltas and prevented the -rate metric from
            // being derived in the drain.
            if (clientType == "producer")
            {
                AddMetric(metrics, string.Concat(topicPrefix, "/record-send-total"), totalTxMessages, KafkaMetricType.Cumulative);
                AddMetric(metrics, string.Concat(topicPrefix, "/byte-total"), totalTxBytes, KafkaMetricType.Cumulative);
            }
            else if (clientType == "consumer")
            {
                AddMetric(metrics, string.Concat(topicPrefix, "/records-consumed-total"), totalRxMessages, KafkaMetricType.Cumulative);
                AddMetric(metrics, string.Concat(topicPrefix, "/bytes-consumed-total"), totalRxBytes, KafkaMetricType.Cumulative);
                if (totalConsumerLag > 0 && partitionCount > 0)
                    AddMetric(metrics, string.Concat(topicPrefix, "/records-lag-avg"), totalConsumerLag / partitionCount, KafkaMetricType.Gauge);
            }
        }
    }

    /// <summary>
    /// Records a metric. Cumulative counters are always recorded (even at zero) so the drain's
    /// delta machinery has a continuous baseline and can produce a rate on the first harvest
    /// where real activity appears. Gauges and window averages are filtered at zero because a
    /// point-in-time zero is not interesting to emit.
    /// </summary>
    private static void AddMetric(Dictionary<string, KafkaMetricValue> metrics, string name, long value, KafkaMetricType metricType)
    {
        if (metricType == KafkaMetricType.Cumulative || value > 0)
        {
            metrics[name] = new KafkaMetricValue(value, metricType);
        }
    }

    /// <summary>
    /// Derives the node label for a broker's metric path.
    ///
    /// librdkafka exposes two different broker entries that both use nodeid=-1:
    ///   - Seed brokers (source="configured") — bootstrap entries before real broker IDs are learned
    ///   - Logical brokers (source="logical") — synthetic handles like GroupCoordinator
    /// The broker Source field distinguishes them. Without this check, GroupCoordinator metrics
    /// silently collide into the "seed" bucket.
    ///
    /// Additionally, some Confluent client versions encode synthetic coordinator broker IDs as
    /// int.MaxValue - brokerId. NodeId values above 1,000,000 are decoded back to coordinator-N.
    /// </summary>
    private static string NormalizeNodeId(KafkaBrokerStats broker)
    {
        if (broker.Source == "logical")
        {
            if (broker.Name == "GroupCoordinator")
                return "coordinator";
            return broker.Name != null ? broker.Name.ToLowerInvariant() : "logical";
        }

        // Confluent-specific encoding: coordinator broker IDs are int.MaxValue - K for small K.
        // Restrict the decode window to within 1000 of int.MaxValue to avoid misclassifying
        // legitimate high node IDs in large clusters.
        const int CoordinatorIdWindow = 1000;

        if (broker.NodeId < 0)
            return "seed";

        if (broker.NodeId > int.MaxValue - CoordinatorIdWindow)
        {
            var coordinatorId = int.MaxValue - broker.NodeId;
            if (coordinatorId > 0)
                return string.Concat("coordinator-", coordinatorId.ToString());
        }

        return broker.NodeId.ToString();
    }
}
