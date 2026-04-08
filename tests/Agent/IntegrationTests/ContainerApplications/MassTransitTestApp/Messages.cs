// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace MassTransitTestApp;

/// <summary>Message type for Kafka Rider transport.</summary>
public class KafkaMessage
{
    public string Text { get; set; }
}

/// <summary>Message type for RabbitMQ bus transport.</summary>
public class RabbitMqMessage
{
    public string Text { get; set; }
}

/// <summary>Message type for InMemory bus transport.</summary>
public class InMemoryMessage
{
    public string Text { get; set; }
}
