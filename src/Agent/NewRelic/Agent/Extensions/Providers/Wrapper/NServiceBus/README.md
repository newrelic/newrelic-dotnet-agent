# New Relic .NET Agent NServiceBus Instrumentation

## Overview

The NServiceBus instrumentation wrapper provides automatic monitoring for NServiceBus message processing operations within existing transactions. It creates message broker segments for message receive and send operations, captures message handler execution, and instruments the pipeline processing for NServiceBus versions 5 and 6+.

## Instrumented Methods

### ReceiveMessageWrapper (NServiceBus 5)
- **Wrapper**: [ReceiveMessageWrapper.cs](ReceiveMessageWrapper.cs)
- **Assembly**: `NServiceBus.Core`
- **Type**: `NServiceBus.InvokeHandlersBehavior`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Invoke | No | Yes |

### SendMessageWrapper (NServiceBus 5)
- **Wrapper**: [SendMessageWrapper.cs](SendMessageWrapper.cs)
- **Assembly**: `NServiceBus.Core`
- **Type**: `NServiceBus.Unicast.UnicastBus`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| SendMessage | No | Yes |

### LoadHandlersConnectorWrapper (NServiceBus 6+)
- **Wrapper**: [LoadHandlersConnectorWrapper.cs](LoadHandlersConnectorWrapper.cs)
- **Assembly**: `NServiceBus.Core`
- **Type**: `NServiceBus.LoadHandlersConnector`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Invoke | No | Yes |

### PipelineWrapper (NServiceBus 6+)
- **Wrapper**: [PipelineWrapper.cs](PipelineWrapper.cs)
- **Assembly**: `NServiceBus.Core`
- **Type**: `NServiceBus.Pipeline\`1`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Invoke | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/NServiceBus/Instrumentation.xml)

## Attributes Added

The wrapper creates message broker segments with the following attributes:

- **Destination type**: Queue or Topic (determined from message context)
- **Broker vendor**: "NServiceBus"
- **Queue/Topic name**: Retrieved from message destination
- **Action**:
  - **Receive operations**: Consume (for `InvokeHandlersBehavior.Invoke`, `LoadHandlersConnector.Invoke`)
  - **Send operations**: Produce (for `UnicastBus.SendMessage`)

## NServiceBus Version Differences

### Version 5 Instrumentation
- **Receive**: Instruments `InvokeHandlersBehavior.Invoke` method
- **Send**: Instruments `UnicastBus.SendMessage` method
- **Context**: Uses `IncomingContext` for receive operations

### Version 6+ Instrumentation
- **Receive**: Instruments `LoadHandlersConnector.Invoke` method
- **Send**: Handled by `Pipeline<T>.Invoke` wrapper
- **Pipeline processing**: Instruments generic `Pipeline<T>.Invoke` for all pipeline stages
- **Context**: Uses `IIncomingLogicalMessageContext` for receive operations

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
