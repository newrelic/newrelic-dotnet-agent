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

            // TODO: this logic may need some tweaking, as some trigger types may not be correctly categorized.
            // The return values are based on https://opentelemetry.io/docs/specs/semconv/attributes-registry/faas/ (scroll to the bottom)
            // TODO: There are many more trigger types that need to be included here. See https://learn.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings?tabs=isolated-process%2Cpython-v2&pivots=programming-language-csharp

            string resolvedTriggerType;

            switch (trigger)
            {
                case "Timer":
                    resolvedTriggerType = "timer";
                    break;

                case "Sql":
                case "Blob":
                case "CosmosDB":
                case "Queue": // Azure Queue storage
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
                    resolvedTriggerType = "pubsub";
                    break;

                case "Http":
                    resolvedTriggerType = "http";
                    break;

                default:
                    resolvedTriggerType = "other";
                    break;
            }

            return resolvedTriggerType;
        }
    }
}
