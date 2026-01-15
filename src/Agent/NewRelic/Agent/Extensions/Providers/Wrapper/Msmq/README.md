# New Relic .NET Agent Msmq Instrumentation

## Overview

The Msmq instrumentation wrapper provides automatic monitoring for Microsoft Message Queue (MSMQ) operations using System.Messaging within an existing transaction. It creates message broker segments for send, receive, and purge operations on MSMQ queues.

## Instrumented Methods

### SendInternalWrapper
- **Wrapper**: [SendInternalWrapper.cs](SendInternalWrapper.cs)
- **Assembly**: `System.Messaging`
- **Type**: `System.Messaging.MessageQueue`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| SendInternal | No | Yes |

### ReceiveCurrentWrapper
- **Wrapper**: [ReceiveCurrentWrapper.cs](ReceiveCurrentWrapper.cs)
- **Assembly**: `System.Messaging`
- **Type**: `System.Messaging.MessageQueue`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ReceiveCurrent | No | Yes |

### PurgeWrapper
- **Wrapper**: [PurgeWrapper.cs](PurgeWrapper.cs)
- **Assembly**: `System.Messaging`
- **Type**: `System.Messaging.MessageQueue`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Purge | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Msmq/Instrumentation.xml)

## Attributes Added

The wrapper creates message broker segments with the following attributes:

- **Destination type**: Queue
- **Broker vendor**: "Msmq"
- **Queue name**: Retrieved from `MessageQueue.QueueName` property
- **Action**:
  - `SendInternal`: Produce
  - `ReceiveCurrent`: Consume
  - `Purge`: Purge

## Instrumented Operations

### Send Operations (SendInternalWrapper)
- **Method**: `SendInternal` (internal method called by public `Send` methods)
- **Action**: Produce
- **Captures**: Queue name from `MessageQueue` instance

### Receive Operations (ReceiveCurrentWrapper)
- **Method**: `ReceiveCurrent` (internal method called by receive operations)
- **Action**: Consume
- **Captures**: Queue name from `MessageQueue` instance

### Purge Operations (PurgeWrapper)
- **Method**: `Purge` (removes all messages from queue)
- **Action**: Purge
- **Captures**: Queue name from `MessageQueue` instance

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
