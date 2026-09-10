// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.Kafka;

internal sealed class KafkaClientInfo
{
    public string BootstrapServers { get; }
    public string[] Servers { get; }

    public KafkaClientInfo(string bootstrapServers)
    {
        BootstrapServers = bootstrapServers;
        Servers = bootstrapServers.Split(',');
    }
}

internal static class KafkaHelper
{
    private static readonly ConditionalWeakTable<object, KafkaClientInfo> _clientInfoCache = new();
    private static readonly object _clientInfoCacheLock = new();

    public static void AddClientToCache(object producerOrConsumerInstance, string bootStrapServers)
    {
        if (string.IsNullOrEmpty(bootStrapServers))
            return;

        var clientInfo = new KafkaClientInfo(bootStrapServers);

        // Remove first: ConditionalWeakTable.Add throws if the key is already present.
        lock (_clientInfoCacheLock)
        {
            _clientInfoCache.Remove(producerOrConsumerInstance);
            _clientInfoCache.Add(producerOrConsumerInstance, clientInfo);
        }
    }

    public static bool TryGetClientInfo(object producerOrConsumerInstance, out KafkaClientInfo clientInfo)
    {
        return _clientInfoCache.TryGetValue(producerOrConsumerInstance, out clientInfo);
    }

    public static void RecordKafkaNodeMetrics(IAgent agent, string topicName, KafkaClientInfo clientInfo, bool isProducer)
    {
        var mode = (isProducer ? "Produce" : "Consume");

        foreach (var server in clientInfo.Servers)
        {
            agent.RecordCountMetric($"MessageBroker/Kafka/Nodes/{server}");
            agent.RecordCountMetric($"MessageBroker/Kafka/Nodes/{server}/{mode}/{topicName}");
        }
    }
}
