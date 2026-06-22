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
    private static readonly ConditionalWeakTable<object, BootstrapEntry> _bootstrapEntryByInstance = new();
    // One AdminClient fetch per unique bootstrap servers string.
    private static readonly ConcurrentDictionary<string, byte> _fetchScheduled = new();
    private static readonly ConcurrentDictionary<string, string> _clusterIdByBootstrap = new();

    private sealed class BootstrapEntry { public string BootstrapServers; }

    public static void ScheduleClusterIdFetch(object producerOrConsumerInstance, string bootstrapServers)
    {
        if (string.IsNullOrEmpty(bootstrapServers)) return;

        _bootstrapEntryByInstance.GetValue(producerOrConsumerInstance, _ => new BootstrapEntry { BootstrapServers = bootstrapServers });

        if (!_fetchScheduled.TryAdd(bootstrapServers, 1)) return;

        Task.Run(async () =>
        {
            int[] delaysMs = { 0, 30000, 60000, 120000, 240000 };
            for (int attempt = 0; attempt < delaysMs.Length; attempt++)
            {
                if (delaysMs[attempt] > 0) await Task.Delay(delaysMs[attempt]);
                try
                {
                    using var adminClient = new AdminClientBuilder(new Dictionary<string, string>
                    {
                        ["bootstrap.servers"] = bootstrapServers,
                        ["socket.timeout.ms"] = "60000",
                        ["request.timeout.ms"] = "60000",
                        ["socket.connection.setup.timeout.ms"] = "60000",
                    }).Build();
                    var result = await adminClient.DescribeClusterAsync(new DescribeClusterOptions { RequestTimeout = TimeSpan.FromSeconds(60) });
                    var clusterId = result?.ClusterId;
                    if (!string.IsNullOrEmpty(clusterId))
                    {
                        _clusterIdByBootstrap[bootstrapServers] = clusterId;
                        return;
                    }
                }
                catch (Exception ex) { Trace.TraceInformation($"New Relic: Kafka cluster ID fetch attempt {attempt + 1} failed: {ex.Message}"); }
            }
        });
    }

    public static bool TryGetClusterIdFromCache(object instance, out string clusterId)
    {
        if (_bootstrapEntryByInstance.TryGetValue(instance, out var entry))
            return _clusterIdByBootstrap.TryGetValue(entry.BootstrapServers, out clusterId);
        clusterId = null;
        return false;
    }
}
