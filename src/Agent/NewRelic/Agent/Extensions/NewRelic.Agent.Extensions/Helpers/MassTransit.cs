// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;


namespace NewRelic.Agent.Extensions.Helpers;

public class MassTransitQueueData
{
    public string QueueName { get; set; } = "Unknown";
    public MessageBrokerDestinationType DestinationType { get; set; } = MessageBrokerDestinationType.Queue;
}

public class MassTransitHelpers
{
    public static MassTransitQueueData GetQueueData(Uri sourceAddress, Uri fallbackAddress = null)
    {
        var data = GetQueueDataFromUri(sourceAddress);

        // If the primary address didn't yield a meaningful name, try the fallback.
        // This handles transports like Kafka Rider where SourceAddress may be the
        // bus endpoint (e.g. loopback://localhost/) while DestinationAddress contains
        // the actual topic/queue information.
        if (data.QueueName == "Unknown" && fallbackAddress != null)
            data = GetQueueDataFromUri(fallbackAddress);

        return data;
    }

    private static MassTransitQueueData GetQueueDataFromUri(Uri sourceAddress)
    {
        var data = new MassTransitQueueData();

        if (sourceAddress == null)
            return data;

        var scheme = sourceAddress.Scheme.ToLowerInvariant();

        // Short-form addressing schemes used by any transport: queue://, topic://, exchange://
        switch (scheme)
        {
            case "topic":
                data.QueueName = GetLastPathSegment(sourceAddress);
                data.DestinationType = MessageBrokerDestinationType.Topic;
                return data;
            case "queue":
                data.QueueName = GetLastPathSegment(sourceAddress);
                data.DestinationType = MessageBrokerDestinationType.Queue;
                return data;
            case "exchange":
                data.QueueName = GetLastPathSegment(sourceAddress);
                data.DestinationType = MessageBrokerDestinationType.Queue;
                return data;
        }

        // MassTransit Rider embeds path prefixes (e.g. /kafka/, /event-hub/) regardless
        // of the bus transport scheme. Check for these before scheme-specific parsing.
        if (TryParseRiderPrefix(sourceAddress, data))
            return data;

        // Transport-specific parsing by URI scheme
        switch (scheme)
        {
            case "kafka":
                data.QueueName = GetLastPathSegment(sourceAddress);
                data.DestinationType = MessageBrokerDestinationType.Topic;
                break;

            case "sb":
                ParseServiceBusUri(sourceAddress, data);
                break;

            case "amazonsqs":
                data.QueueName = GetLastPathSegment(sourceAddress);
                data.DestinationType = GetDestinationTypeFromQueryParam(sourceAddress);
                if (data.DestinationType == MessageBrokerDestinationType.Queue && HasQueryParam(sourceAddress, "temporary", "true"))
                    data.DestinationType = MessageBrokerDestinationType.TempQueue;
                break;

            case "activemq":
            case "amqp":
            case "amqps":
                data.QueueName = GetLastPathSegment(sourceAddress);
                data.DestinationType = GetDestinationTypeFromQueryParam(sourceAddress);
                if (data.DestinationType == MessageBrokerDestinationType.Queue && HasQueryParam(sourceAddress, "temporary", "true"))
                    data.DestinationType = MessageBrokerDestinationType.TempQueue;
                break;

            case "rabbitmq":
                ParseRabbitMqUri(sourceAddress, data);
                break;

            case "loopback":
                data.QueueName = GetLastPathSegment(sourceAddress);
                break;

            default:
                // Graceful fallback for unknown transports: try last path segment
                data.QueueName = GetLastPathSegment(sourceAddress);
                break;
        }

        return data;
    }

    private static bool TryParseRiderPrefix(Uri sourceAddress, MassTransitQueueData data)
    {
        // MassTransit Rider transports (Kafka, Event Hubs) embed a path prefix
        // in the URI regardless of the underlying bus transport scheme.
        // e.g. loopback://localhost/kafka/{topic}, rabbitmq://host/kafka/{topic}
        var path = sourceAddress.AbsolutePath.TrimStart('/');

        if (path.StartsWith("kafka/", StringComparison.OrdinalIgnoreCase))
        {
            data.QueueName = path.Substring("kafka/".Length).TrimEnd('/');
            data.DestinationType = MessageBrokerDestinationType.Topic;
            return true;
        }

        if (path.StartsWith("event-hub/", StringComparison.OrdinalIgnoreCase))
        {
            data.QueueName = path.Substring("event-hub/".Length).TrimEnd('/');
            data.DestinationType = MessageBrokerDestinationType.Topic;
            return true;
        }

        return false;
    }

    private static void ParseRabbitMqUri(Uri sourceAddress, MassTransitQueueData data)
    {
        // RabbitMQ uses underscore-delimited names:
        // rabbitmq://localhost/SomeHostname_MassTransitTest_bus_queuename?temporary=true
        var items = sourceAddress.AbsoluteUri.Split('_');
        if (items.Length > 1)
        {
            var queueData = items[items.Length - 1].Split('?');
            data.QueueName = queueData[0];
            if (queueData.Length == 2 && queueData[1] == "temporary=true")
            {
                data.DestinationType = MessageBrokerDestinationType.TempQueue;
            }
        }
        else
        {
            // No underscores — fall back to last path segment
            data.QueueName = GetLastPathSegment(sourceAddress);
            if (HasQueryParam(sourceAddress, "temporary", "true"))
                data.DestinationType = MessageBrokerDestinationType.TempQueue;
        }
    }

    private static void ParseServiceBusUri(Uri sourceAddress, MassTransitQueueData data)
    {
        // Azure Event Hubs: sb://ns.servicebus.windows.net/event-hub/my-hub
        var path = sourceAddress.AbsolutePath.TrimStart('/');
        if (path.StartsWith("event-hub/", StringComparison.OrdinalIgnoreCase))
        {
            data.QueueName = path.Substring("event-hub/".Length).TrimEnd('/');
            data.DestinationType = MessageBrokerDestinationType.Topic;
        }
        else
        {
            // Azure Service Bus queue or topic: sb://ns.servicebus.windows.net/my-queue
            data.QueueName = GetLastPathSegment(sourceAddress);
            data.DestinationType = GetDestinationTypeFromQueryParam(sourceAddress);
            if (data.DestinationType == MessageBrokerDestinationType.Queue && HasQueryParam(sourceAddress, "autodelete"))
                data.DestinationType = MessageBrokerDestinationType.TempQueue;
        }
    }

    private static string GetLastPathSegment(Uri uri)
    {
        var segments = uri.Segments;
        if (segments == null || segments.Length == 0)
            return "Unknown";

        // Walk backwards to find the last non-empty segment
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var segment = segments[i].TrimEnd('/');
            if (!string.IsNullOrEmpty(segment))
                return Uri.UnescapeDataString(segment);
        }

        return "Unknown";
    }

    private static MessageBrokerDestinationType GetDestinationTypeFromQueryParam(Uri uri)
    {
        if (HasQueryParam(uri, "type", "topic"))
            return MessageBrokerDestinationType.Topic;

        return MessageBrokerDestinationType.Queue;
    }

    private static bool HasQueryParam(Uri uri, string key, string value = null)
    {
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
            return false;

        // Strip leading '?'
        query = query.TrimStart('?');

        foreach (var pair in query.Split('&'))
        {
            var parts = pair.Split('=');
            if (parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                if (value == null)
                    return true;
                if (parts.Length > 1 && parts[1].Equals(value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}