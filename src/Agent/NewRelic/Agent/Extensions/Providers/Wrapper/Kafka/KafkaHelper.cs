// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.Kafka;

internal static class KafkaHelper
{
    private static readonly ConditionalWeakTable<object, List<string>> _bootstrapServerCache = new();

    public static void AddBootstrapServersToCache(object producerOrConsumerInstance, string bootStrapServers)
    {
        if (string.IsNullOrEmpty(bootStrapServers))
            return;
        var kafkaBootstrapServers = new List<string>();
        var servers = bootStrapServers.Split(',');
        foreach (var server in servers)
            kafkaBootstrapServers.Add(server);
        _bootstrapServerCache.GetValue(producerOrConsumerInstance, _ => kafkaBootstrapServers);
    }

    public static bool TryGetBootstrapServersFromCache(object producerOrConsumerInstance, out List<string> kafkaBootstrapServers)
        => _bootstrapServerCache.TryGetValue(producerOrConsumerInstance, out kafkaBootstrapServers);

    public static void RecordKafkaNodeMetrics(IAgent agent, string topicName, List<string> bootstrapServers, bool isProducer)
    {
        foreach (var server in bootstrapServers)
        {
            var mode = isProducer ? "Produce" : "Consume";
            agent.RecordCountMetric($"MessageBroker/Kafka/Nodes/{server}");
            agent.RecordCountMetric($"MessageBroker/Kafka/Nodes/{server}/{mode}/{topicName}");
        }
    }

    private const long ClusterIdTtlMs = 60L * 60 * 1000;

    private sealed class ClusterIdEntry
    {
        public string ClusterId;
        public long StoredMs; // Environment.TickCount64
    }

    private sealed class BootstrapEntry { public string BootstrapServers; }

    // ConditionalWeakTable maps each instance to its bootstrapServers string; entries auto-evict when GC'd.
    private static readonly ConditionalWeakTable<object, BootstrapEntry> _bootstrapEntryByInstance = new();
    // Guards against duplicate concurrent fetches for the same bootstrap servers string.
    private static readonly ConcurrentDictionary<string, byte> _fetchScheduled = new();
    // Resolved cluster IDs, with the timestamp when they were stored (for TTL checks).
    private static readonly ConcurrentDictionary<string, ClusterIdEntry> _clusterIdByBootstrap = new();
    // Full producer/consumer config retained so expired entries can be re-fetched without a new instance.
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _configByBootstrap = new();

    public static void ScheduleClusterIdFetch(object producerOrConsumerInstance, string bootstrapServers, Dictionary<string, string> fullConfig)
    {
        if (string.IsNullOrEmpty(bootstrapServers)) return;

        // Normalize ordering so "b:9092,a:9092" and "a:9092,b:9092" hit the same cache slot.
        var parts = bootstrapServers.Split(',');
        Array.Sort(parts);
        bootstrapServers = string.Join(",", parts);

        _bootstrapEntryByInstance.GetValue(producerOrConsumerInstance, _ => new BootstrapEntry { BootstrapServers = bootstrapServers });
        _configByBootstrap.TryAdd(bootstrapServers, fullConfig);

        if (!_fetchScheduled.TryAdd(bootstrapServers, 1)) return;

        StartClusterIdFetchTask(bootstrapServers, fullConfig);
    }

    private static void StartClusterIdFetchTask(string bootstrapServers, Dictionary<string, string> fullConfig)
    {
        Task.Run(async () =>
        {
            int[] delaysMs = { 0, 30000, 60000, 120000, 240000 };
            for (int attempt = 0; attempt < delaysMs.Length; attempt++)
            {
                if (delaysMs[attempt] > 0) await Task.Delay(delaysMs[attempt]);
                try
                {
                    var adminConfig = new Dictionary<string, string>(fullConfig)
                    {
                        ["bootstrap.servers"] = bootstrapServers,
                        ["socket.timeout.ms"] = "60000",
                        ["request.timeout.ms"] = "60000",
                        ["socket.connection.setup.timeout.ms"] = "60000",
                    };
                    using var adminClient = new AdminClientBuilder(adminConfig).Build();
                    var result = await adminClient.DescribeClusterAsync(new DescribeClusterOptions { RequestTimeout = TimeSpan.FromSeconds(60) });
                    var clusterId = result?.ClusterId;
                    if (!string.IsNullOrEmpty(clusterId))
                    {
                        _clusterIdByBootstrap[bootstrapServers] = new ClusterIdEntry { ClusterId = clusterId, StoredMs = Environment.TickCount64 };
                        _fetchScheduled.TryRemove(bootstrapServers, out _);
                        return;
                    }
                }
                catch (Exception ex) { Trace.TraceInformation($"New Relic: Kafka cluster ID fetch attempt {attempt + 1} failed: {ex.Message}"); }
            }
            // All retries exhausted — clear guard so TTL expiry can trigger a future retry.
            _fetchScheduled.TryRemove(bootstrapServers, out _);
        });
    }

    public static bool TryGetClusterIdFromCache(object instance, out string clusterId)
    {
        if (!_bootstrapEntryByInstance.TryGetValue(instance, out var entry))
        {
            clusterId = null;
            return false;
        }

        if (!_clusterIdByBootstrap.TryGetValue(entry.BootstrapServers, out var cacheEntry))
        {
            clusterId = null;
            return false;
        }

        // TTL check — kick off a background re-fetch; return stale value while it's in progress.
        if (Environment.TickCount64 - cacheEntry.StoredMs > ClusterIdTtlMs)
        {
            if (_configByBootstrap.TryGetValue(entry.BootstrapServers, out var savedConfig) &&
                _fetchScheduled.TryAdd(entry.BootstrapServers, 1))
            {
                StartClusterIdFetchTask(entry.BootstrapServers, savedConfig);
            }
        }

        clusterId = cacheEntry.ClusterId;
        return true;
    }
}
