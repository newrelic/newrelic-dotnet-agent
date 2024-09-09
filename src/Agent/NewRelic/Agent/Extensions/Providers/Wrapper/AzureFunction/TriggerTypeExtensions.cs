// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Providers.Wrapper.AzureFunction
{
    public static class TriggerTypeExtensions
    {
        public static string ResolveTriggerType(this string triggerTypeName)
        {
            // triggerTypeName is a short typename; we want everything to the left of "TriggerAttribute"
            var trigger = triggerTypeName.Substring(0, triggerTypeName.IndexOf("TriggerAttribute", StringComparison.Ordinal));

            // The return values are based on https://opentelemetry.io/docs/specs/semconv/attributes-registry/faas/ (scroll to the bottom)
            // 08/27/2024 - All trigger types added from https://learn.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings?tabs=isolated-process%2Cpython-v2&pivots=programming-language-csharp

            string resolvedTriggerType;

            switch (trigger)
            {
                case "Timer":
                    resolvedTriggerType = "timer";
                    break;

                case "Sql":
                case "Blob":
                case "CosmosDB":
                case "Queue": // Azure Queue storage - group with other "Storage" triggers
                case "DaprBinding": // "events to and from external source such as databases, queues, file systems, etc." - fits datasource best
                    resolvedTriggerType = "datasource";
                    break;

                case "Kafka":
                case "SignalR":
                case "EventGrid":
                case "EventHub":
                case "ServiceBus":
                case "RabbitMQ":
                case "RedisPubSub":
                case "RedisList":
                case "RedisStream":
                case "WebPubSub":
                case "DaprTopic": //subscription to a Dapr topic - grouping with other "PubSub" triggers
                    resolvedTriggerType = "pubsub";
                    break;

                case "Http":
                    resolvedTriggerType = "http";
                    break;

                case "DaprServiceInvocation": // RPC call to another Dapr service - no group so other.
                    resolvedTriggerType = "other";
                    break;
                default:
                    resolvedTriggerType = "other";
                    break;
            }

            return resolvedTriggerType;
        }
    }
}
