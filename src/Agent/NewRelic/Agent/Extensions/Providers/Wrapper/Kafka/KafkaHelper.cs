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

    // ConditionalWeakTable maps each instance to its bootstrapServers string; entries auto-evict when GC'd.
    private static readonly ConditionalWeakTable<object, string> _bootstrapServersByInstance = new();
    // Guards against duplicate concurrent fetches for the same bootstrap servers string.
    private static readonly ConcurrentDictionary<string, byte> _fetchScheduled = new();
    // Resolved cluster IDs. Cached forever once resolved — a cluster id is a stable identifier
    // for the lifetime of a broker cluster; on failure, nothing is cached, so the next
    // Producer/Consumer built against the same bootstrap servers retries naturally.
    private static readonly ConcurrentDictionary<string, string> _clusterIdByBootstrap = new();

    public static void ScheduleClusterIdFetch(object producerOrConsumerInstance, string bootstrapServers)
    {
        if (string.IsNullOrEmpty(bootstrapServers)) return;

        // Normalize ordering so "b:9092,a:9092" and "a:9092,b:9092" hit the same cache slot.
        var parts = bootstrapServers.Split(',');
        Array.Sort(parts);
        bootstrapServers = string.Join(",", parts);

        _bootstrapServersByInstance.GetValue(producerOrConsumerInstance, _ => bootstrapServers);

        if (_clusterIdByBootstrap.ContainsKey(bootstrapServers)) return; // already resolved
        if (!_fetchScheduled.TryAdd(bootstrapServers, 1)) return; // fetch already in flight

        Handle handle;
        try
        {
            // Producer<TKey,TValue> and Consumer<TKey,TValue> both implement the non-generic
            // IClient interface, so this doesn't need reflection despite the instance arriving
            // here as `object` (its TKey/TValue are only known to customer code).
            handle = ((IClient)producerOrConsumerInstance).Handle;
        }
        catch (Exception ex)
        {
            Trace.TraceInformation($"New Relic: Could not read Kafka client handle for cluster ID fetch: {ex.Message}");
            _fetchScheduled.TryRemove(bootstrapServers, out _);
            return;
        }

        StartClusterIdFetchTask(bootstrapServers, handle);
    }

    private static void StartClusterIdFetchTask(string bootstrapServers, Handle handle)
    {
        Task.Run(async () =>
        {
            try
            {
                // DependentAdminClientBuilder reuses the producer/consumer's already-connected
                // native handle — no second connection is opened.
                using var adminClient = new DependentAdminClientBuilder(handle).Build();
                var result = await adminClient.DescribeClusterAsync(new DescribeClusterOptions { RequestTimeout = TimeSpan.FromSeconds(5) });
                var clusterId = result?.ClusterId;
                if (!string.IsNullOrEmpty(clusterId))
                {
                    _clusterIdByBootstrap[bootstrapServers] = clusterId;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceInformation($"New Relic: Kafka cluster ID fetch failed: {ex.Message}");
            }
            finally
            {
                _fetchScheduled.TryRemove(bootstrapServers, out _);
            }
        });
    }

    public static bool TryGetClusterIdFromCache(object instance, out string clusterId)
    {
        if (!_bootstrapServersByInstance.TryGetValue(instance, out var bootstrapServers))
        {
            clusterId = null;
            return false;
        }

        return _clusterIdByBootstrap.TryGetValue(bootstrapServers, out clusterId);
    }
}
