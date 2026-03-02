// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NewRelic.Agent.Extensions.Helpers;

/// <summary>
/// Helper for parsing and extracting metrics from Kafka statistics JSON.
/// This helper is in the Extensions project so it has access to Newtonsoft.Json.
/// </summary>
public static class KafkaStatisticsHelper
{
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

    /// <summary>
    /// Comprehensive data structure for passing parsed Kafka metrics back to the wrapper.
    /// Avoids the wrapper needing to deal with JSON parsing or complex models.
    /// </summary>
    public class KafkaMetricsData
    {
        // Basic client identification
        public string ClientId { get; set; }
        public string ClientType { get; set; }

        public long RequestCount { get; set; }
        public long ResponseCount { get; set; }
        public long TxMessages { get; set; }
        public long RxMessages { get; set; }
        public long TxBytes { get; set; }
        public long RxBytes { get; set; }
        public long MetadataCacheCount { get; set; }

        public long MessageQueueCount { get; set; }
        public long MessageQueueSize { get; set; }
        public long TotalTxBytes { get; set; }
        public long TotalRxBytes { get; set; }

        // Consumer group metrics (for consumers only)
        public KafkaConsumerMetrics ConsumerMetrics { get; set; }

        // Producer idempotent metrics (for producers only)
        public KafkaProducerMetrics ProducerMetrics { get; set; }

        // Broker-level metrics aggregated across all brokers
        public List<KafkaBrokerMetrics> BrokerMetrics { get; set; } = new List<KafkaBrokerMetrics>();

        // Topic-level metrics (when available)
        public List<KafkaTopicMetrics> TopicMetrics { get; set; } = new List<KafkaTopicMetrics>();

        // Partition-level metrics (when available)
        public List<KafkaPartitionMetrics> PartitionMetrics { get; set; } = new List<KafkaPartitionMetrics>();

        public bool IsValid => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientType);
    }

    /// <summary>
    /// Consumer-specific metrics
    /// </summary>
    public class KafkaConsumerMetrics
    {
        public string GroupState { get; set; }
        public long RebalanceCount { get; set; }
        public long RebalanceAge { get; set; }
        public int AssignedPartitions { get; set; }
        public string LastRebalanceReason { get; set; }
        public long TotalConsumerLag { get; set; }
    }

    /// <summary>
    /// Producer-specific metrics
    /// </summary>
    public class KafkaProducerMetrics
    {
        public string IdempotentState { get; set; }
        public long ProducerId { get; set; }
        public int ProducerEpoch { get; set; }
        public long EpochCount { get; set; }
        public long BatchSizeAvg { get; set; }
        public long BatchCountAvg { get; set; }
        public long RecordsPerRequestAvg { get; set; }
    }

    /// <summary>
    /// Broker-level metrics
    /// </summary>
    public class KafkaBrokerMetrics
    {
        public string BrokerName { get; set; }
        public int NodeId { get; set; }
        public string State { get; set; }
        public long Requests { get; set; }
        public long Responses { get; set; }
        public long OutgoingBytes { get; set; }
        public long IncomingBytes { get; set; }
        public long RequestErrors { get; set; }
        public long ResponseErrors { get; set; }
        public long ConnectionCount { get; set; }
        public long DisconnectCount { get; set; }
        public long RoundTripTimeAvg { get; set; }
    }

    /// <summary>
    /// Topic-level metrics
    /// </summary>
    public class KafkaTopicMetrics
    {
        public string TopicName { get; set; }
        public long MetadataAge { get; set; }
        public long BatchSizeAvg { get; set; }
        public long BatchCountAvg { get; set; }
        public int PartitionCount { get; set; }
        public long TotalTxMessages { get; set; }
        public long TotalRxMessages { get; set; }
        public long TotalConsumerLag { get; set; }
    }

    /// <summary>
    /// Partition-level metrics
    /// </summary>
    public class KafkaPartitionMetrics
    {
        public string TopicName { get; set; }
        public int PartitionId { get; set; }
        public int BrokerId { get; set; }
        public int LeaderId { get; set; }
        public long TxMessages { get; set; }
        public long RxMessages { get; set; }
        public long TxBytes { get; set; }
        public long RxBytes { get; set; }
        public long ConsumerLag { get; set; }
        public long LowWatermark { get; set; }
        public long HighWatermark { get; set; }
        public long CommittedOffset { get; set; }
    }

    /// <summary>
    /// Parses Kafka statistics JSON and extracts comprehensive metrics for New Relic reporting.
    /// Returns a comprehensive data structure that the wrapper can use without JSON dependencies.
    /// </summary>
    /// <param name="statisticsJson">JSON statistics from librdkafka</param>
    /// <returns>KafkaMetricsData with parsed metrics, or null if parsing fails</returns>
    public static KafkaMetricsData ParseStatistics(string statisticsJson)
    {
        if (string.IsNullOrEmpty(statisticsJson))
            return null;

        try
        {
            var stats = JsonConvert.DeserializeObject<KafkaStatistics>(statisticsJson);
            if (stats == null)
                return null;

            var metricsData = new KafkaMetricsData
            {
                // Basic identification (use client_id if available, fallback to name)
                ClientId = !string.IsNullOrEmpty(stats.ClientId) ? stats.ClientId : stats.Name,
                ClientType = stats.Type,
                RequestCount = stats.Tx,
                ResponseCount = stats.Rx,
                TxMessages = stats.TxMsgs,
                RxMessages = stats.RxMsgs,
                TxBytes = stats.TxMsgBytes,
                RxBytes = stats.RxMsgBytes,
                MetadataCacheCount = stats.MetadataCacheCnt,
                MessageQueueCount = stats.MsgCnt,
                MessageQueueSize = stats.MsgSize,
                TotalTxBytes = stats.TxBytes,
                TotalRxBytes = stats.RxBytes
            };

            // Parse consumer-specific metrics (only for consumers)
            if (stats.Type == "consumer" && stats.ConsumerGroup != null)
            {
                metricsData.ConsumerMetrics = ParseConsumerMetrics(stats);
            }

            // Parse producer-specific metrics (only for producers)
            if (stats.Type == "producer")
            {
                metricsData.ProducerMetrics = ParseProducerMetrics(stats);
            }

            // Parse broker-level metrics
            metricsData.BrokerMetrics = ParseBrokerMetrics(stats.Brokers);

            // Parse topic-level metrics
            metricsData.TopicMetrics = ParseTopicMetrics(stats.Topics);

            // Parse partition-level metrics
            metricsData.PartitionMetrics = ParsePartitionMetrics(stats.Topics);

            // Calculate aggregated consumer lag across all partitions
            metricsData.ConsumerMetrics = metricsData.ConsumerMetrics ?? new KafkaConsumerMetrics();
            metricsData.ConsumerMetrics.TotalConsumerLag = metricsData.PartitionMetrics.Sum(p => p.ConsumerLag);

            return metricsData;
        }
        catch (Exception)
        {
            // Return null on any parsing error - wrapper will handle this gracefully
            return null;
        }
    }

    private static KafkaConsumerMetrics ParseConsumerMetrics(KafkaStatistics stats)
    {
        var cgrp = stats.ConsumerGroup;
        if (cgrp == null) return null;

        return new KafkaConsumerMetrics
        {
            GroupState = cgrp.State,
            RebalanceCount = cgrp.RebalanceCount,
            RebalanceAge = cgrp.RebalanceAge,
            AssignedPartitions = cgrp.AssignmentSize,
            LastRebalanceReason = cgrp.RebalanceReason
        };
    }

    private static KafkaProducerMetrics ParseProducerMetrics(KafkaStatistics stats)
    {
        var producerMetrics = new KafkaProducerMetrics();

        // EOS/Idempotent metrics
        if (stats.ProducerEos != null)
        {
            producerMetrics.IdempotentState = stats.ProducerEos.IdempotentState;
            producerMetrics.ProducerId = stats.ProducerEos.ProducerId;
            producerMetrics.ProducerEpoch = stats.ProducerEos.ProducerEpoch;
            producerMetrics.EpochCount = stats.ProducerEos.EpochCount;
        }

        // Calculate batch metrics across all topics
        var batchSizes = new List<long>();
        var batchCounts = new List<long>();

        foreach (var topic in stats.Topics.Values)
        {
            if (topic.BatchSize != null)
                batchSizes.Add(topic.BatchSize.Avg);
            if (topic.BatchCount != null)
                batchCounts.Add(topic.BatchCount.Avg);
        }

        if (batchSizes.Any())
        {
            producerMetrics.BatchSizeAvg = (long)batchSizes.Average();
            producerMetrics.RecordsPerRequestAvg = producerMetrics.BatchSizeAvg > 0 ? stats.TxMsgs / producerMetrics.BatchSizeAvg : 0;
        }

        if (batchCounts.Any())
        {
            producerMetrics.BatchCountAvg = (long)batchCounts.Average();
        }

        return producerMetrics;
    }

    private static List<KafkaBrokerMetrics> ParseBrokerMetrics(Dictionary<string, KafkaBrokerStats> brokers)
    {
        var brokerMetrics = new List<KafkaBrokerMetrics>();

        if (brokers == null) return brokerMetrics;

        foreach (var broker in brokers.Values)
        {
            brokerMetrics.Add(new KafkaBrokerMetrics
            {
                BrokerName = broker.Name,
                NodeId = broker.NodeId,
                State = broker.State,
                Requests = broker.Tx,
                Responses = broker.Rx,
                OutgoingBytes = broker.TxBytes,
                IncomingBytes = broker.RxBytes,
                RequestErrors = broker.TxErrs,
                ResponseErrors = broker.RxErrs,
                ConnectionCount = broker.Connects,
                DisconnectCount = broker.Disconnects,
                RoundTripTimeAvg = broker.RoundTripTime?.Avg ?? 0
            });
        }

        return brokerMetrics;
    }

    private static List<KafkaTopicMetrics> ParseTopicMetrics(Dictionary<string, KafkaTopicStats> topics)
    {
        var topicMetrics = new List<KafkaTopicMetrics>();

        if (topics == null) return topicMetrics;

        foreach (var topic in topics.Values)
        {
            var totalTxMessages = 0L;
            var totalRxMessages = 0L;
            var totalConsumerLag = 0L;
            var partitionCount = topic.Partitions?.Count ?? 0;

            // Aggregate partition metrics for topic totals
            if (topic.Partitions != null)
            {
                foreach (var partition in topic.Partitions.Values)
                {
                    totalTxMessages += partition.TxMsgs;
                    totalRxMessages += partition.RxMsgs;
                    totalConsumerLag += partition.ConsumerLag;
                }
            }

            topicMetrics.Add(new KafkaTopicMetrics
            {
                TopicName = topic.Topic,
                MetadataAge = topic.MetadataAge,
                BatchSizeAvg = topic.BatchSize?.Avg ?? 0,
                BatchCountAvg = topic.BatchCount?.Avg ?? 0,
                PartitionCount = partitionCount,
                TotalTxMessages = totalTxMessages,
                TotalRxMessages = totalRxMessages,
                TotalConsumerLag = totalConsumerLag
            });
        }

        return topicMetrics;
    }

    private static List<KafkaPartitionMetrics> ParsePartitionMetrics(Dictionary<string, KafkaTopicStats> topics)
    {
        var partitionMetrics = new List<KafkaPartitionMetrics>();

        if (topics == null) return partitionMetrics;

        foreach (var topic in topics.Values)
        {
            if (topic.Partitions == null) continue;

            foreach (var partition in topic.Partitions.Values)
            {
                partitionMetrics.Add(new KafkaPartitionMetrics
                {
                    TopicName = topic.Topic,
                    PartitionId = partition.Partition,
                    BrokerId = partition.Broker,
                    LeaderId = partition.Leader,
                    TxMessages = partition.TxMsgs,
                    RxMessages = partition.RxMsgs,
                    TxBytes = partition.TxBytes,
                    RxBytes = partition.RxBytes,
                    ConsumerLag = partition.ConsumerLag,
                    LowWatermark = partition.LowWatermark,
                    HighWatermark = partition.HighWatermark,
                    CommittedOffset = partition.CommittedOffset
                });
            }
        }

        return partitionMetrics;
    }

    /// <summary>
    /// Creates comprehensive metric names using the New Relic Kafka internal metrics format.
    /// Follows Java agent MetricNameUtil.java naming conventions for maximum parity.
    /// Returns a dictionary of metric names to values for easy consumption by wrappers.
    /// </summary>
    /// <param name="metricsData">Parsed Kafka metrics data</param>
    /// <param name="vendorName">Kafka vendor name (should use MessageBrokerVendorConstants.Kafka)</param>
    /// <returns>Dictionary of metric names to values</returns>
    public static Dictionary<string, long> CreateMetricsDictionary(KafkaMetricsData metricsData, string vendorName = "Kafka")
    {
        var metrics = new Dictionary<string, long>();

        if (metricsData?.IsValid != true)
            return metrics;

        // Generate client-level metrics
        AddClientLevelMetrics(metrics, metricsData, vendorName);

        // Generate consumer-specific metrics
        AddConsumerMetrics(metrics, metricsData, vendorName);

        // Generate producer-specific metrics
        AddProducerMetrics(metrics, metricsData, vendorName);

        // Generate broker-level metrics
        AddBrokerMetrics(metrics, metricsData, vendorName);

        // Generate topic-level metrics
        AddTopicMetrics(metrics, metricsData, vendorName);

        // Generate partition-level metrics
        AddPartitionMetrics(metrics, metricsData, vendorName);

        return metrics;
    }

    private static void AddClientLevelMetrics(Dictionary<string, long> metrics, KafkaMetricsData data, string vendorName)
    {
        var clientType = data.ClientType;
        var clientId = data.ClientId;
        var basePrefix = $"MessageBroker/{vendorName}/Internal/{clientType}-metrics/client/{clientId}";

        AddMetricIfPositive(metrics, $"{basePrefix}/request-counter", data.RequestCount);
        AddMetricIfPositive(metrics, $"{basePrefix}/response-counter", data.ResponseCount);
        AddMetricIfPositive(metrics, $"{basePrefix}/txmsgs", data.TxMessages);
        AddMetricIfPositive(metrics, $"{basePrefix}/rxmsgs", data.RxMessages);
        AddMetricIfPositive(metrics, $"{basePrefix}/txmsg_bytes", data.TxBytes);
        AddMetricIfPositive(metrics, $"{basePrefix}/rxmsg_bytes", data.RxBytes);
        AddMetricIfPositive(metrics, $"{basePrefix}/metadata_cache_cnt", data.MetadataCacheCount);
        AddMetricIfPositive(metrics, $"{basePrefix}/record-queue-time-avg", data.MessageQueueCount);
        AddMetricIfPositive(metrics, $"{basePrefix}/record-size-avg", data.MessageQueueCount > 0 ? data.MessageQueueSize / data.MessageQueueCount : 0);
        AddMetricIfPositive(metrics, $"{basePrefix}/outgoing-byte-total", data.TotalTxBytes);
        AddMetricIfPositive(metrics, $"{basePrefix}/incoming-byte-total", data.TotalRxBytes);
        AddMetricIfPositive(metrics, $"{basePrefix}/request-total", data.RequestCount);
        AddMetricIfPositive(metrics, $"{basePrefix}/response-total", data.ResponseCount);
    }

    private static void AddConsumerMetrics(Dictionary<string, long> metrics, KafkaMetricsData data, string vendorName)
    {
        if (data.ClientType != "consumer" || data.ConsumerMetrics == null) return;

        var clientId = data.ClientId;
        var consumerMetrics = data.ConsumerMetrics;

        // Consumer coordinator metrics (matches Java agent pattern)
        var coordinatorPrefix = $"MessageBroker/{vendorName}/Internal/consumer-coordinator-metrics/client/{clientId}";
        AddMetricIfPositive(metrics, $"{coordinatorPrefix}/rebalance-total", consumerMetrics.RebalanceCount);
        AddMetricIfPositive(metrics, $"{coordinatorPrefix}/rebalance-latency-avg", consumerMetrics.RebalanceAge);
        AddMetricIfPositive(metrics, $"{coordinatorPrefix}/assigned-partitions", consumerMetrics.AssignedPartitions);

        // Consumer fetch manager metrics
        var fetchPrefix = $"MessageBroker/{vendorName}/Internal/consumer-fetch-manager-metrics/client/{clientId}";
        AddMetricIfPositive(metrics, $"{fetchPrefix}/records-consumed-total", data.RxMessages);
        AddMetricIfPositive(metrics, $"{fetchPrefix}/bytes-consumed-total", data.RxBytes);
        AddMetricIfPositive(metrics, $"{fetchPrefix}/records-lag-avg", consumerMetrics.TotalConsumerLag > 0 && consumerMetrics.AssignedPartitions > 0
            ? consumerMetrics.TotalConsumerLag / consumerMetrics.AssignedPartitions : 0);
        AddMetricIfPositive(metrics, $"{fetchPrefix}/records-lag-max", consumerMetrics.TotalConsumerLag);
    }

    private static void AddProducerMetrics(Dictionary<string, long> metrics, KafkaMetricsData data, string vendorName)
    {
        if (data.ClientType != "producer" || data.ProducerMetrics == null) return;

        var clientId = data.ClientId;
        var producerMetrics = data.ProducerMetrics;

        // Producer metrics (matches Java agent pattern)
        var producerPrefix = $"MessageBroker/{vendorName}/Internal/producer-metrics/client/{clientId}";
        AddMetricIfPositive(metrics, $"{producerPrefix}/batch-size-avg", producerMetrics.BatchSizeAvg);
        AddMetricIfPositive(metrics, $"{producerPrefix}/records-per-request-avg", producerMetrics.RecordsPerRequestAvg);
        AddMetricIfPositive(metrics, $"{producerPrefix}/record-send-total", data.TxMessages);
        AddMetricIfPositive(metrics, $"{producerPrefix}/record-error-total", 0); // librdkafka doesn't provide this directly

        // Idempotent producer metrics
        if (!string.IsNullOrEmpty(producerMetrics.IdempotentState))
        {
            AddMetricIfPositive(metrics, $"{producerPrefix}/producer-id-changes", producerMetrics.EpochCount);
        }
    }

    private static void AddBrokerMetrics(Dictionary<string, long> metrics, KafkaMetricsData data, string vendorName)
    {
        if (data.BrokerMetrics == null) return;

        foreach (var broker in data.BrokerMetrics)
        {
            // Normalize node ID following Java agent pattern (handle negative/coordinator nodes)
            var normalizedNodeId = NormalizeNodeId(broker.NodeId.ToString());
            var nodePrefix = $"MessageBroker/{vendorName}/Internal/{data.ClientType}-node-metrics/node/{normalizedNodeId}/client/{data.ClientId}";

            AddMetricIfPositive(metrics, $"{nodePrefix}/request-total", broker.Requests);
            AddMetricIfPositive(metrics, $"{nodePrefix}/response-total", broker.Responses);
            AddMetricIfPositive(metrics, $"{nodePrefix}/outgoing-byte-total", broker.OutgoingBytes);
            AddMetricIfPositive(metrics, $"{nodePrefix}/incoming-byte-total", broker.IncomingBytes);
            AddMetricIfPositive(metrics, $"{nodePrefix}/request-latency-avg", broker.RoundTripTimeAvg);
            AddMetricIfPositive(metrics, $"{nodePrefix}/connection-count", broker.ConnectionCount);
        }
    }

    private static void AddTopicMetrics(Dictionary<string, long> metrics, KafkaMetricsData data, string vendorName)
    {
        if (data.TopicMetrics == null) return;

        foreach (var topic in data.TopicMetrics)
        {
            var topicPrefix = $"MessageBroker/{vendorName}/Internal/{data.ClientType}-topic-metrics/topic/{topic.TopicName}/client/{data.ClientId}";

            if (data.ClientType == "producer")
            {
                AddMetricIfPositive(metrics, $"{topicPrefix}/record-send-total", topic.TotalTxMessages);
                AddMetricIfPositive(metrics, $"{topicPrefix}/byte-total", topic.TotalTxMessages * topic.BatchSizeAvg);
                AddMetricIfPositive(metrics, $"{topicPrefix}/record-error-total", 0); // Not directly available
            }
            else if (data.ClientType == "consumer")
            {
                AddMetricIfPositive(metrics, $"{topicPrefix}/records-consumed-total", topic.TotalRxMessages);
                AddMetricIfPositive(metrics, $"{topicPrefix}/bytes-consumed-total", topic.TotalRxMessages * 1000); // Estimate
                AddMetricIfPositive(metrics, $"{topicPrefix}/records-lag-avg", topic.TotalConsumerLag > 0 && topic.PartitionCount > 0
                    ? topic.TotalConsumerLag / topic.PartitionCount : 0);
            }
        }
    }

    private static void AddPartitionMetrics(Dictionary<string, long> metrics, KafkaMetricsData data, string vendorName)
    {
        if (data.PartitionMetrics == null) return;

        foreach (var partition in data.PartitionMetrics)
        {
            // Follow Java agent pattern: topic/partition/client hierarchy
            var partitionPrefix = $"MessageBroker/{vendorName}/Internal/{data.ClientType}-metrics/topic/{partition.TopicName}/partition/{partition.PartitionId}/client/{data.ClientId}";

            if (data.ClientType == "consumer")
            {
                AddMetricIfPositive(metrics, $"{partitionPrefix}/records-consumed-total", partition.RxMessages);
                AddMetricIfPositive(metrics, $"{partitionPrefix}/bytes-consumed-total", partition.RxBytes);
                AddMetricIfPositive(metrics, $"{partitionPrefix}/records-lag", partition.ConsumerLag);
                AddMetricIfPositive(metrics, $"{partitionPrefix}/committed-offset", partition.CommittedOffset);
                AddMetricIfPositive(metrics, $"{partitionPrefix}/position", partition.HighWatermark);
            }
            else if (data.ClientType == "producer")
            {
                AddMetricIfPositive(metrics, $"{partitionPrefix}/record-send-total", partition.TxMessages);
                AddMetricIfPositive(metrics, $"{partitionPrefix}/byte-total", partition.TxBytes);
            }
        }
    }

    private static void AddMetricIfPositive(Dictionary<string, long> metrics, string name, long value)
    {
        if (value > 0)
        {
            metrics[name] = value;
        }
    }

    /// <summary>
    /// Normalizes node ID following Java agent MetricNameUtil pattern
    /// </summary>
    private static string NormalizeNodeId(string nodeId)
    {
        // Handle seed brokers (negative IDs)
        if (int.TryParse(nodeId, out var num) && num < 0)
        {
            return "seed";
        }

        // Handle coordinator nodes (very large positive numbers)
        if (num > 1000000) // Approximation for coordinator detection
        {
            var coordinatorId = int.MaxValue - num;
            if (coordinatorId > 0 && coordinatorId < 1000)
            {
                return $"coordinator-{coordinatorId}";
            }
        }

        return nodeId;
    }
}
