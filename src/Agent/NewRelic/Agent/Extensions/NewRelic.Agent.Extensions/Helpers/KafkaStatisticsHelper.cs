// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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
        public Dictionary<string, KafkaBrokerStats> Brokers { get; set; } = new Dictionary<string, KafkaBrokerStats>();

        [JsonProperty("topics")]
        public Dictionary<string, KafkaTopicStats> Topics { get; set; } = new Dictionary<string, KafkaTopicStats>();

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
        public Dictionary<string, KafkaPartitionStats> Partitions { get; set; } = new Dictionary<string, KafkaPartitionStats>();
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
    /// Creates a new dictionary of metric names to values from parsed statistics.
    /// </summary>
    public static Dictionary<string, KafkaMetricValue> CreateMetricsDictionary(KafkaStatistics stats, string vendorName = "Kafka")
    {
        var metrics = new Dictionary<string, KafkaMetricValue>();
        PopulateMetricsDictionary(metrics, stats, vendorName);
        return metrics;
    }

    /// <summary>
    /// Populates an existing dictionary with metrics from parsed statistics.
    /// Clears the dictionary first, reusing its internal storage.
    /// </summary>
    public static void PopulateMetricsDictionary(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string vendorName = "Kafka")
    {
        metrics.Clear();

        if (!IsValid(stats))
            return;

        var clientId = GetClientId(stats);
        var clientType = stats.Type;

        AddClientLevelMetrics(metrics, stats, clientType, clientId, vendorName);

        if (clientType == "consumer" && stats.ConsumerGroup != null)
            AddConsumerMetrics(metrics, stats, clientId, vendorName);

        if (clientType == "producer")
            AddProducerMetrics(metrics, stats, clientId, vendorName);

        AddBrokerMetrics(metrics, stats, clientType, clientId, vendorName);
        AddTopicAndPartitionMetrics(metrics, stats, clientType, clientId, vendorName);
    }

    private static void AddClientLevelMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientType, string clientId, string vendorName)
    {
        var basePrefix = string.Concat("MessageBroker/", vendorName, "/Internal/", clientType, "-metrics/client/", clientId);

        // Cumulative counters (librdkafka "int" type — ever-increasing)
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/request-counter"), stats.Tx, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/response-counter"), stats.Rx, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/txmsgs"), stats.TxMsgs, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/rxmsgs"), stats.RxMsgs, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/txmsg_bytes"), stats.TxMsgBytes, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/rxmsg_bytes"), stats.RxMsgBytes, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/outgoing-byte-total"), stats.TxBytes, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/incoming-byte-total"), stats.RxBytes, KafkaMetricType.Cumulative);

        // Gauges (librdkafka "int gauge" type — point-in-time snapshot)
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/metadata_cache_cnt"), stats.MetadataCacheCnt, KafkaMetricType.Gauge);
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/record-queue-time-avg"), stats.MsgCnt, KafkaMetricType.Gauge);
        AddMetricIfPositive(metrics, string.Concat(basePrefix, "/record-size-avg"), stats.MsgCnt > 0 ? stats.MsgSize / stats.MsgCnt : 0, KafkaMetricType.Gauge);
    }

    private static void AddConsumerMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientId, string vendorName)
    {
        var cgrp = stats.ConsumerGroup;
        var coordinatorPrefix = string.Concat("MessageBroker/", vendorName, "/Internal/consumer-coordinator-metrics/client/", clientId);
        AddMetricIfPositive(metrics, string.Concat(coordinatorPrefix, "/rebalance-total"), cgrp.RebalanceCount, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(coordinatorPrefix, "/rebalance-latency-avg"), cgrp.RebalanceAge, KafkaMetricType.Gauge);
        AddMetricIfPositive(metrics, string.Concat(coordinatorPrefix, "/assigned-partitions"), cgrp.AssignmentSize, KafkaMetricType.Gauge);

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

        var fetchPrefix = string.Concat("MessageBroker/", vendorName, "/Internal/consumer-fetch-manager-metrics/client/", clientId);
        AddMetricIfPositive(metrics, string.Concat(fetchPrefix, "/records-consumed-total"), stats.RxMsgs, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(fetchPrefix, "/bytes-consumed-total"), stats.RxMsgBytes, KafkaMetricType.Cumulative);
        AddMetricIfPositive(metrics, string.Concat(fetchPrefix, "/records-lag-avg"),
            totalConsumerLag > 0 && cgrp.AssignmentSize > 0 ? totalConsumerLag / cgrp.AssignmentSize : 0, KafkaMetricType.Gauge);
        AddMetricIfPositive(metrics, string.Concat(fetchPrefix, "/records-lag-max"), totalConsumerLag, KafkaMetricType.Gauge);
    }

    private static void AddProducerMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientId, string vendorName)
    {
        var producerPrefix = string.Concat("MessageBroker/", vendorName, "/Internal/producer-metrics/client/", clientId);

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

        var batchSizeAvg = batchSizeCount > 0 ? batchSizeSum / batchSizeCount : 0;
        var recordsPerRequestAvg = batchSizeAvg > 0 ? stats.TxMsgs / batchSizeAvg : 0;

        AddMetricIfPositive(metrics, string.Concat(producerPrefix, "/batch-size-avg"), batchSizeAvg, KafkaMetricType.WindowAvg);
        AddMetricIfPositive(metrics, string.Concat(producerPrefix, "/records-per-request-avg"), recordsPerRequestAvg, KafkaMetricType.WindowAvg);
        AddMetricIfPositive(metrics, string.Concat(producerPrefix, "/record-send-total"), stats.TxMsgs, KafkaMetricType.Cumulative);

        // Idempotent producer metrics
        if (stats.ProducerEos != null && !string.IsNullOrEmpty(stats.ProducerEos.IdempotentState))
        {
            AddMetricIfPositive(metrics, string.Concat(producerPrefix, "/producer-id-changes"), stats.ProducerEos.EpochCount, KafkaMetricType.Cumulative);
        }
    }

    private static void AddBrokerMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientType, string clientId, string vendorName)
    {
        if (stats.Brokers == null) return;

        foreach (var broker in stats.Brokers.Values)
        {
            var normalizedNodeId = NormalizeNodeId(broker.NodeId);
            var nodePrefix = string.Concat("MessageBroker/", vendorName, "/Internal/", clientType, "-node-metrics/node/", normalizedNodeId, "/client/", clientId);

            AddMetricIfPositive(metrics, string.Concat(nodePrefix, "/request-total"), broker.Tx, KafkaMetricType.Cumulative);
            AddMetricIfPositive(metrics, string.Concat(nodePrefix, "/response-total"), broker.Rx, KafkaMetricType.Cumulative);
            AddMetricIfPositive(metrics, string.Concat(nodePrefix, "/outgoing-byte-total"), broker.TxBytes, KafkaMetricType.Cumulative);
            AddMetricIfPositive(metrics, string.Concat(nodePrefix, "/incoming-byte-total"), broker.RxBytes, KafkaMetricType.Cumulative);
            AddMetricIfPositive(metrics, string.Concat(nodePrefix, "/request-latency-avg"), broker.RoundTripTime?.Avg ?? 0, KafkaMetricType.WindowAvg);
            AddMetricIfPositive(metrics, string.Concat(nodePrefix, "/connection-count"), broker.Connects, KafkaMetricType.Cumulative);
        }
    }

    /// <summary>
    /// Single-pass topic and partition metric generation. Avoids iterating partitions twice.
    /// </summary>
    private static void AddTopicAndPartitionMetrics(Dictionary<string, KafkaMetricValue> metrics, KafkaStatistics stats, string clientType, string clientId, string vendorName)
    {
        if (stats.Topics == null) return;

        foreach (var topic in stats.Topics.Values)
        {
            var topicPrefix = string.Concat("MessageBroker/", vendorName, "/Internal/", clientType, "-topic-metrics/topic/", topic.Topic, "/client/", clientId);
            var partitionBasePrefix = string.Concat("MessageBroker/", vendorName, "/Internal/", clientType, "-metrics/topic/", topic.Topic);

            // Aggregate partition stats and emit partition metrics in a single pass
            long totalTxMessages = 0, totalRxMessages = 0, totalRxBytes = 0, totalConsumerLag = 0;
            var partitionCount = 0;

            if (topic.Partitions != null)
            {
                foreach (var partition in topic.Partitions.Values)
                {
                    partitionCount++;
                    totalTxMessages += partition.TxMsgs;
                    totalRxMessages += partition.RxMsgs;
                    totalRxBytes += partition.RxBytes;
                    totalConsumerLag += partition.ConsumerLag;

                    // Emit partition-level metrics inline
                    var partitionPrefix = string.Concat(partitionBasePrefix, "/partition/", partition.Partition.ToString(), "/client/", clientId);

                    if (clientType == "consumer")
                    {
                        AddMetricIfPositive(metrics, string.Concat(partitionPrefix, "/records-consumed-total"), partition.RxMsgs, KafkaMetricType.Cumulative);
                        AddMetricIfPositive(metrics, string.Concat(partitionPrefix, "/bytes-consumed-total"), partition.RxBytes, KafkaMetricType.Cumulative);
                        AddMetricIfPositive(metrics, string.Concat(partitionPrefix, "/records-lag"), partition.ConsumerLag, KafkaMetricType.Gauge);
                        AddMetricIfPositive(metrics, string.Concat(partitionPrefix, "/committed-offset"), partition.CommittedOffset, KafkaMetricType.Gauge);
                        AddMetricIfPositive(metrics, string.Concat(partitionPrefix, "/position"), partition.HighWatermark, KafkaMetricType.Gauge);
                    }
                    else if (clientType == "producer")
                    {
                        AddMetricIfPositive(metrics, string.Concat(partitionPrefix, "/record-send-total"), partition.TxMsgs, KafkaMetricType.Cumulative);
                        AddMetricIfPositive(metrics, string.Concat(partitionPrefix, "/byte-total"), partition.TxBytes, KafkaMetricType.Cumulative);
                    }
                }
            }

            // Emit topic-level metrics using aggregated partition data
            if (clientType == "producer")
            {
                AddMetricIfPositive(metrics, string.Concat(topicPrefix, "/record-send-total"), totalTxMessages, KafkaMetricType.Cumulative);
                AddMetricIfPositive(metrics, string.Concat(topicPrefix, "/byte-total"), totalTxMessages * (topic.BatchSize?.Avg ?? 0), KafkaMetricType.Cumulative);
            }
            else if (clientType == "consumer")
            {
                AddMetricIfPositive(metrics, string.Concat(topicPrefix, "/records-consumed-total"), totalRxMessages, KafkaMetricType.Cumulative);
                AddMetricIfPositive(metrics, string.Concat(topicPrefix, "/bytes-consumed-total"), totalRxBytes, KafkaMetricType.Cumulative);
                AddMetricIfPositive(metrics, string.Concat(topicPrefix, "/records-lag-avg"),
                    totalConsumerLag > 0 && partitionCount > 0 ? totalConsumerLag / partitionCount : 0, KafkaMetricType.Gauge);
            }
        }
    }

    private static void AddMetricIfPositive(Dictionary<string, KafkaMetricValue> metrics, string name, long value, KafkaMetricType metricType)
    {
        if (value > 0)
        {
            metrics[name] = new KafkaMetricValue(value, metricType);
        }
    }

    /// <summary>
    /// Normalizes node ID following Java agent MetricNameUtil pattern
    /// </summary>
    private static string NormalizeNodeId(int nodeId)
    {
        if (nodeId < 0)
            return "seed";

        if (nodeId > 1000000)
        {
            var coordinatorId = int.MaxValue - nodeId;
            if (coordinatorId > 0 && coordinatorId < 1000)
                return string.Concat("coordinator-", coordinatorId.ToString());
        }

        return nodeId.ToString();
    }
}
