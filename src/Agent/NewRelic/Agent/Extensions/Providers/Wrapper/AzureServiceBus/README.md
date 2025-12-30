# New Relic .NET Agent AzureServiceBus Instrumentation

## Overview

The AzureServiceBus instrumentation wrapper provides automatic monitoring for Azure Service Bus operations using the Azure.Messaging.ServiceBus library. It instruments message sending, receiving, processing, and management operations to create message broker segments and transactions with comprehensive tracing across Azure Service Bus interactions.

## Instrumented Methods

### AzureServiceBusReceiveWrapper
- **Wrapper**: [AzureServiceBusReceiveWrapper.cs](AzureServiceBusReceiveWrapper.cs)
- **Assembly**: `Azure.Messaging.ServiceBus`
- **Type**: `Azure.Messaging.ServiceBus.ServiceBusReceiver`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| [ReceiveMessagesAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Receiver/ServiceBusReceiver.cs) | No | Yes |
| [ReceiveDeferredMessagesAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Receiver/ServiceBusReceiver.cs) | No | Yes |
| [PeekMessagesInternalAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Receiver/ServiceBusReceiver.cs) | No | Yes |
| [CompleteMessageAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Receiver/ServiceBusReceiver.cs) | No | Yes |
| [AbandonMessageAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Receiver/ServiceBusReceiver.cs) | No | Yes |
| [DeadLetterInternalAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Receiver/ServiceBusReceiver.cs) | No | Yes |
| [DeferMessageAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Receiver/ServiceBusReceiver.cs) | No | Yes |
| [RenewMessageLockAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Receiver/ServiceBusReceiver.cs) | No | Yes |

### AzureServiceBusSendWrapper
- **Wrapper**: [AzureServiceBusSendWrapper.cs](AzureServiceBusSendWrapper.cs)
- **Assembly**: `Azure.Messaging.ServiceBus`
- **Type**: `Azure.Messaging.ServiceBus.ServiceBusSender`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| [SendMessagesAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Sender/ServiceBusSender.cs) | No | Yes |
| [ScheduleMessagesAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Sender/ServiceBusSender.cs) | No | Yes |
| [CancelScheduledMessagesAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Sender/ServiceBusSender.cs) | No | Yes |

### AzureServiceBusProcessorWrapper
- **Wrapper**: [AzureServiceBusProcessorWrapper.cs](AzureServiceBusProcessorWrapper.cs)
- **Assembly**: `Azure.Messaging.ServiceBus`
- **Type**: `Azure.Messaging.ServiceBus.ServiceBusProcessor`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| [OnProcessMessageAsync](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Processor/ServiceBusProcessor.cs) | No | Yes |

### AzureServiceBusReceiverManagerWrapper
- **Wrapper**: [AzureServiceBusReceiverManagerWrapper.cs](AzureServiceBusReceiverManagerWrapper.cs)
- **Assembly**: `Azure.Messaging.ServiceBus`
- **Type**: `Azure.Messaging.ServiceBus.ReceiverManager`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| [ProcessOneMessage](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/src/Processor/ReceiverManager.cs) | Yes | No |

## Configuration

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AzureServiceBus/Instrumentation.xml)

## Transaction Lifecycle

### Transaction Creation
- **AzureServiceBusReceiverManagerWrapper** creates new transactions for message processing operations, enabling end-to-end tracing of Service Bus message workflows.

### Transaction Enrichment
- **AzureServiceBusReceiveWrapper**, **AzureServiceBusSendWrapper**, and **AzureServiceBusProcessorWrapper** require existing transactions and add message broker segments to provide detailed timing and metadata.

## Attributes Added

The wrapper adds the following attributes to message broker segments:

- **messaging.system**: Set to "ServiceBus"
- **messaging.destination.name**: Queue or topic name (subscription path removed for topics)
- **messaging.operation**: Set to "consume", "produce", "peek", "settle", "cancel", or "process" based on operation type
- **server.address**: Fully qualified namespace (e.g., "some-service-bus-entity.servicebus.windows.net")

## Trigger/Type Resolution

The wrapper uses destination name analysis to determine message broker destination type:

- **Queue**: Default type for destinations without "Subscriptions" in the name
- **Topic**: Destinations containing "/Subscriptions/" in the entity path

Operations are mapped to OpenTelemetry messaging actions:
- `ReceiveMessagesAsync`, `ReceiveDeferredMessagesAsync` → consume
- `PeekMessagesInternalAsync` → peek
- `CompleteMessageAsync`, `AbandonMessageAsync`, `DeadLetterInternalAsync`, `DeferMessageAsync` → settle
- `SendMessagesAsync`, `ScheduleMessagesAsync` → produce
- `CancelScheduledMessagesAsync` → cancel
- `ProcessOneMessage` → process

## Distributed Tracing

### Header Insertion (Publishing)
- **AzureServiceBusSendWrapper** inserts distributed tracing headers into message `ApplicationProperties` for outbound messages during produce operations
- Headers are inserted as key-value pairs in the message properties dictionary

### Header Extraction (Consuming)
- **AzureServiceBusReceiveWrapper** and **AzureServiceBusReceiverManagerWrapper** extract distributed tracing headers from message `ApplicationProperties`
- Uses `transaction.AcceptDistributedTraceHeaders()` with transport type `Queue`
- For multi-message operations, headers are extracted from the first message in the collection

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0