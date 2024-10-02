// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Generic;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.Kafka
{
    internal static class KafkaHelper
    {
        private static readonly ConcurrentDictionary<object, List<string>> _bootstrapServerCache = new();

        public static void AddBootstrapServersToCache(object producerOrConsumerInstance, string bootStrapServers)
        {
            if (string.IsNullOrEmpty(bootStrapServers))
                return;
            var kafkaBootstrapServers = new List<string>();

            // parse bootStrapServers - it's a comma separated list of host:port pairs
            var servers = bootStrapServers.Split(',');
            foreach (var server in servers)
            {
                kafkaBootstrapServers.Add(server);
            }

            _bootstrapServerCache[producerOrConsumerInstance] = kafkaBootstrapServers;
        }

        public static bool TryGetBootstrapServersFromCache(object producerOrConsumerInstance, out List<string> kafkaBootstrapServers)
        {
            return _bootstrapServerCache.TryGetValue(producerOrConsumerInstance, out kafkaBootstrapServers);
        }

        public static void RecordKafkaNodeMetrics(IAgent agent, string topicName, List<string> bootstrapServers, bool isProducer)
        {
            foreach (var server in bootstrapServers)
            {
                var mode = (isProducer? "Produce" : "Consume");

                agent.RecordCountMetric($"MessageBroker/Kafka/Nodes/{server}");
                agent.RecordCountMetric($"MessageBroker/Kafka/Nodes/{server}/{mode}/{topicName}");
            }
            
        }
    }
}
