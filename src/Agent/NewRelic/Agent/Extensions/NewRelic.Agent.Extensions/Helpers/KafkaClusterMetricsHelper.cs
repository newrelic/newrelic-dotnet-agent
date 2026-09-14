// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Helpers;

public enum KafkaClusterOperation
{
    Produce,
    Consume
}

public delegate bool ClusterIdLookup(string bootstrapServers, out string clusterId);

public static class KafkaClusterMetricsHelper
{
    private const string ClusterIdAttributeName = "kafka.cluster.id";

    public static void RecordClusterMetrics(IAgent agent, ISegment segment, string bootstrapServers, string topic, KafkaClusterOperation operation, ClusterIdLookup clusterIdLookup)
    {
        if (!agent.Configuration.KafkaClusterMetricsEnabled)
            return;

        if (!clusterIdLookup(bootstrapServers, out var clusterId) || string.IsNullOrEmpty(clusterId))
            return;

        var mode = operation == KafkaClusterOperation.Produce ? "Produce" : "Consume";

        agent.RecordCountMetric($"MessageBroker/Kafka/Cluster/{clusterId}/{mode}/{topic}");
        segment.AddCustomAttribute(ClusterIdAttributeName, clusterId);
    }
}
