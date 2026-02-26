// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
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
    /// Based on the official librdkafka statistics documentation.
    /// </summary>
    public class KafkaStatistics
    {
        [JsonProperty("name")]
        public string Name { get; set; }

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
    }

    /// <summary>
    /// Simple data structure for passing parsed Kafka metrics back to the wrapper.
    /// Avoids the wrapper needing to deal with JSON parsing or complex models.
    /// </summary>
    public class KafkaMetricsData
    {
        public string ClientId { get; set; }
        public string ClientType { get; set; }
        public long RequestCount { get; set; }
        public long ResponseCount { get; set; }
        public long TxMessages { get; set; }
        public long RxMessages { get; set; }
        public long TxBytes { get; set; }
        public long RxBytes { get; set; }
        public long MetadataCacheCount { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientType);
    }

    /// <summary>
    /// Parses Kafka statistics JSON and extracts key metrics for New Relic reporting.
    /// Returns a simple data structure that the wrapper can use without JSON dependencies.
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

            return new KafkaMetricsData
            {
                ClientId = stats.Name,
                ClientType = stats.Type,
                RequestCount = stats.Tx,
                ResponseCount = stats.Rx,
                TxMessages = stats.TxMsgs,
                RxMessages = stats.RxMsgs,
                TxBytes = stats.TxMsgBytes,
                RxBytes = stats.RxMsgBytes,
                MetadataCacheCount = stats.MetadataCacheCnt
            };
        }
        catch (Exception)
        {
            // Return null on any parsing error - wrapper will handle this gracefully
            return null;
        }
    }

    /// <summary>
    /// Creates metric names using the New Relic Kafka internal metrics format.
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

        var clientType = metricsData.ClientType;
        var clientId = metricsData.ClientId;

        // Critical UI metrics
        if (metricsData.RequestCount > 0)
        {
            metrics[$"MessageBroker/{vendorName}/Internal/{clientType}-metrics/client/{clientId}/request-counter"] = metricsData.RequestCount;
        }

        if (metricsData.ResponseCount > 0)
        {
            metrics[$"MessageBroker/{vendorName}/Internal/{clientType}-metrics/client/{clientId}/response-counter"] = metricsData.ResponseCount;
        }

        // Additional metrics
        if (metricsData.TxMessages > 0)
        {
            metrics[$"MessageBroker/{vendorName}/Internal/{clientType}-metrics/client/{clientId}/txmsgs"] = metricsData.TxMessages;
        }

        if (metricsData.RxMessages > 0)
        {
            metrics[$"MessageBroker/{vendorName}/Internal/{clientType}-metrics/client/{clientId}/rxmsgs"] = metricsData.RxMessages;
        }

        if (metricsData.TxBytes > 0)
        {
            metrics[$"MessageBroker/{vendorName}/Internal/{clientType}-metrics/client/{clientId}/txmsg_bytes"] = metricsData.TxBytes;
        }

        if (metricsData.RxBytes > 0)
        {
            metrics[$"MessageBroker/{vendorName}/Internal/{clientType}-metrics/client/{clientId}/rxmsg_bytes"] = metricsData.RxBytes;
        }

        if (metricsData.MetadataCacheCount > 0)
        {
            metrics[$"MessageBroker/{vendorName}/Internal/{clientType}-metrics/client/{clientId}/metadata_cache_cnt"] = metricsData.MetadataCacheCount;
        }

        return metrics;
    }
}