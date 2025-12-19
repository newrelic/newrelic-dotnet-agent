# New Relic .NET Agent Kafka Instrumentation

## Overview

The Kafka instrumentation wrapper provides automatic monitoring for Apache Kafka message broker operations using the Confluent.Kafka .NET client. It creates message broker segments for produce and consume operations, tracks serialization timing, manages distributed tracing headers, and records metrics for message throughput and Kafka node interactions.

## Instrumented Methods

### KafkaProducerWrapper
- **Wrapper**: [KafkaProducerWrapper.cs](KafkaProducerWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.Producer\`2`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Produce | No | Yes |
| ProduceAsync | No | Yes |

### KafkaConsumerWrapper
- **Wrapper**: [KafkaConsumerWrapper.cs](KafkaConsumerWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.Consumer\`2`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Consume | No | Yes |

### KafkaSerializerWrapper (Utf8Serializer)
- **Wrapper**: [KafkaSerializerWrapper.cs](KafkaSerializerWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.Serializers+Utf8Serializer`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Serialize | No | Yes |

### KafkaSerializerWrapper (NullSerializer)
- **Wrapper**: [KafkaSerializerWrapper.cs](KafkaSerializerWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.Serializers+NullSerializer`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Serialize | No | Yes |

### KafkaSerializerWrapper (Int64Serializer)
- **Wrapper**: [KafkaSerializerWrapper.cs](KafkaSerializerWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.Serializers+Int64Serializer`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Serialize | No | Yes |

### KafkaSerializerWrapper (Int32Serializer)
- **Wrapper**: [KafkaSerializerWrapper.cs](KafkaSerializerWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.Serializers+Int32Serializer`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Serialize | No | Yes |

### KafkaSerializerWrapper (SingleSerializer)
- **Wrapper**: [KafkaSerializerWrapper.cs](KafkaSerializerWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.Serializers+SingleSerializer`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Serialize | No | Yes |

### KafkaSerializerWrapper (DoubleSerializer)
- **Wrapper**: [KafkaSerializerWrapper.cs](KafkaSerializerWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.Serializers+DoubleSerializer`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Serialize | No | Yes |

### KafkaSerializerWrapper (ByteArraySerializer)
- **Wrapper**: [KafkaSerializerWrapper.cs](KafkaSerializerWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.Serializers+ByteArraySerializer`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Serialize | No | Yes |

### KafkaBuilderWrapper (ProducerBuilder)
- **Wrapper**: [KafkaBuilderWrapper.cs](KafkaBuilderWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.ProducerBuilder\`2`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Build | No | No |

### KafkaBuilderWrapper (ConsumerBuilder)
- **Wrapper**: [KafkaBuilderWrapper.cs](KafkaBuilderWrapper.cs)
- **Assembly**: `Confluent.Kafka`
- **Type**: `Confluent.Kafka.ConsumerBuilder\`2`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Build | No | No |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Kafka/Instrumentation.xml)

## Attributes Added

The wrapper creates message broker segments and adds the following attributes:

### Producer Attributes
- **Segment type**: Message broker produce segment
- **Destination type**: Topic
- **Broker vendor**: "Kafka"
- **Topic name**: Retrieved from `TopicPartition.Topic`

### Consumer Attributes
- **Segment type**: Message broker consume segment
- **Destination type**: Topic
- **Broker vendor**: "Kafka"
- **Topic name**: Retrieved from `ConsumeResult.Topic`
- **Custom attribute**: `kafka.consume.byteCount` - Total size in bytes of consumed message (headers + key + value)

### Serialization Attributes
- **Segment type**: Message broker serialization segment
- **Destination type**: Topic
- **Action**: Produce
- **Broker vendor**: "Kafka"
- **Topic name**: Retrieved from `SerializationContext.Topic`
- **Component**: Retrieved from `SerializationContext.Component` (e.g., "Key" or "Value")

## Distributed Tracing

### Producer Header Insertion (KafkaProducerWrapper)

1. **Timing**: Headers are inserted before the message is sent
2. **Carrier**: `MessageMetadata` object (contains `Headers` property)
3. **Method**: `Transaction.InsertDistributedTraceHeaders` adds trace context headers to message headers
4. **Encoding**: Headers are encoded as ASCII bytes and added to `Message.Headers` collection
5. **Duplicate handling**: Existing headers with the same key are removed before adding new ones

### Consumer Header Processing (KafkaConsumerWrapper)

1. **Timing**: Headers are processed after receiving the message from `Consume` operation
2. **Carrier**: `MessageMetadata` object extracted from `ConsumeResult.Message`
3. **Method**: `Transaction.AcceptDistributedTraceHeaders` reads trace context headers from message headers
4. **Transport type**: Set to `TransportType.Kafka`
5. **Encoding**: Header values are decoded from bytes using UTF-8 encoding
6. **Multiple headers**: Supports multiple headers with the same key (returns all matching values)

## Metrics Recorded

### Kafka Node Metrics (Producer and Consumer)

For each bootstrap server configured:
- `MessageBroker/Kafka/Nodes/{server}`: Count of operations per node
- `MessageBroker/Kafka/Nodes/{server}/Produce/{topic}`: Count of produce operations per node and topic
- `MessageBroker/Kafka/Nodes/{server}/Consume/{topic}`: Count of consume operations per node and topic

### Consumer Throughput Metrics

- `Message/Kafka/Topic/Named/{topic}/Received/Messages`: Count of messages received per topic
- `Message/Kafka/Topic/Named/{topic}/Received/Bytes`: Total bytes received per topic (headers + key + value)

### Bootstrap Server Resolution

Bootstrap servers are:
1. Extracted from builder configuration (`bootstrap.servers` key) during `ProducerBuilder.Build()` or `ConsumerBuilder.Build()`
2. Cached in a concurrent dictionary keyed by producer/consumer instance
3. Retrieved during produce/consume operations to record node metrics

## Consume Overload Handling

The `Consume` method has two overloads:
1. **`Consume(int millisecondsTimeout)`**: Creates a standard segment
2. **`Consume(CancellationToken cancellationToken)`**: Creates a leaf segment to prevent nested segments

The `CancellationToken` overload internally calls the `int` overload in a loop until cancelled or a message is received. The leaf segment prevents creating multiple segments for each internal call.

## Serialization Segment Naming

Serialization segments are named with the pattern:
```
MessageBroker/Kafka/Topic/Named/{topic_name}/Serialization/{component}
```

Where `{component}` is either "Key" or "Value" depending on which part of the message is being serialized.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0