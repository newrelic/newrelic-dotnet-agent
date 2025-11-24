# RabbitMQ Instrumentation Wrapper

## Overview

The RabbitMQ instrumentation wrapper provides automatic monitoring for RabbitMQ.Client library operations including message publishing, consuming, and queue management. It creates message broker segments and transactions for comprehensive tracing of AMQP messaging operations across RabbitMQ client interactions.

## Instrumented Methods

### BasicGetWrapper
- **Wrapper**: [BasicGetWrapper.cs](BasicGetWrapper.cs)
- **Assembly**: `RabbitMQ.Client`
- **Type**: `RabbitMQ.Client.Framing.Impl.Model`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| `_Private_BasicGet` | No | Yes | 6.8.1 |

### BasicPublishWrapper
- **Wrapper**: [BasicPublishWrapper.cs](BasicPublishWrapper.cs)
- **Assembly**: `RabbitMQ.Client`
- **Type**: `RabbitMQ.Client.Framing.Impl.Model`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| `_Private_BasicPublish` | No | Yes | 6.8.1 |

### BasicPublishWrapperLegacy
- **Wrapper**: [BasicPublishWrapperLegacy.cs](BasicPublishWrapperLegacy.cs)
- **Assembly**: `RabbitMQ.Client`
- **Type**: `RabbitMQ.Client.Framing.Impl.Model`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| `_Private_BasicPublish` | No | Yes | 6.8.1 |

### HandleBasicDeliverWrapper
- **Wrapper**: [HandleBasicDeliverWrapper.cs](HandleBasicDeliverWrapper.cs)
- **Assembly**: `RabbitMQ.Client`
- **Type**: `RabbitMQ.Client.Events.EventingBasicConsumer`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| `HandleBasicDeliver` | Yes | No | 6.8.1 |

### QueuePurgeWrapper
- **Wrapper**: [QueuePurgeWrapper.cs](QueuePurgeWrapper.cs)
- **Assembly**: `RabbitMQ.Client`
- **Type**: `RabbitMQ.Client.Framing.Impl.Model`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| `_Private_QueuePurge` | No | Yes | 6.8.1 |

## Configuration

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/RabbitMq/Instrumentation.xml)

## Transaction Lifecycle

### Transaction Creation
- **HandleBasicDeliverWrapper** creates new transactions for incoming message consumption, enabling end-to-end tracing of message processing workflows.

### Transaction Enrichment
- **BasicGetWrapper**, **BasicPublishWrapper**, **BasicPublishWrapperLegacy**, and **QueuePurgeWrapper** require existing transactions and add message broker segments to provide detailed timing and metadata.

## Attributes Added

The wrapper adds the following attributes to message broker segments:

- **server.address**: RabbitMQ server hostname (extracted via reflection)
- **server.port**: RabbitMQ server port (extracted via reflection)
- **messaging.destination.name**: Queue name or routing key (null for temporary queues/topics)
- **messaging.destination.routing_key**: Message routing key (when available)
- **messaging.system**: Set to "RabbitMQ"
- **messaging.operation**: Set to "consume", "produce", or "purge"

## Distributed Tracing

### Header Insertion (Publishing)
- **BasicPublishWrapper** and **BasicPublishWrapperLegacy** insert distributed tracing headers into message properties for outbound messages
- Headers are inserted into the `IBasicProperties.Headers` dictionary as string values
- Uses `transaction.InsertDistributedTraceHeaders()` to add W3C Trace Context and New Relic headers

### Header Extraction (Consuming)
- **HandleBasicDeliverWrapper** extracts distributed tracing headers from incoming messages
- Uses `agent.CurrentTransaction.AcceptDistributedTraceHeaders()` with transport type `AMQP`
- Supports W3C Trace Context and New Relic proprietary headers
- Headers are decoded from byte arrays to UTF-8 strings

## Version Considerations

All wrappers target RabbitMQ.Client with `maxVersion="6.8.1"`. The implementation includes version-specific handling:

- **RabbitMQ v6+**: Uses `CreateSegmentForPublishWrappers6Plus()` with type-safe header access via reflection
- **RabbitMQ v5 and earlier**: Uses `CreateSegmentForPublishWrappers()` with dynamic typing for header manipulation
- **Connection extraction**: Different reflection strategies for AutorecoveringModel based on version (v5 uses `m_connection` field, v6+ uses `_connection` field)

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0