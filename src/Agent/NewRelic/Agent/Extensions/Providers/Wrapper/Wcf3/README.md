# New Relic .NET Agent Wcf3 Instrumentation

## Overview

The Wcf3 instrumentation wrapper provides automatic monitoring for Windows Communication Foundation (WCF) services and clients. It creates transactions for WCF service method invocations, tracks client-side service calls as external segments, manages distributed tracing for cross-service communication, and handles both synchronous and asynchronous service operations.

## Instrumented Methods

### WcfIgnoreOuterTransactionWrapper (WCF 4)
- **Wrapper**: [WcfIgnoreOuterTransactionWrapper.cs](WcfIgnoreOuterTransactionWrapper.cs)
- **Assembly**: `System.ServiceModel.Activation`
- **Type**: `System.ServiceModel.Activation.HostedHttpRequestAsyncResult`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| .ctor (constructor) | No | No |

### WcfIgnoreOuterTransactionWrapper (WCF 3)
- **Wrapper**: [WcfIgnoreOuterTransactionWrapper.cs](WcfIgnoreOuterTransactionWrapper.cs)
- **Assembly**: `System.ServiceModel`
- **Type**: `System.ServiceModel.Activation.HostedHttpRequestAsyncResult`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| .ctor (constructor) | No | No |

### ServiceChannelProxyWrapper
- **Wrapper**: [ServiceChannelProxyWrapper.cs](ServiceChannelProxyWrapper.cs)
- **Assembly**: `System.ServiceModel`
- **Type**: `System.ServiceModel.Channels.ServiceChannelProxy`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Invoke | No | Yes |

### ChannelFactoryWrapper
- **Wrapper**: [ChannelFactoryWrapper.cs](ChannelFactoryWrapper.cs)
- **Assembly**: `System.ServiceModel`
- **Type**: `System.ServiceModel.ChannelFactory`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InitializeEndpoint | No | No |

### MethodInvokerWrapper (SyncMethodInvoker)
- **Wrapper**: [MethodInvokerWrapper.cs](MethodInvokerWrapper.cs)
- **Assembly**: `System.ServiceModel`
- **Type**: `System.ServiceModel.Dispatcher.SyncMethodInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Invoke | No | Yes |

### MethodInvokerWrapper (AsyncMethodInvoker)
- **Wrapper**: [MethodInvokerWrapper.cs](MethodInvokerWrapper.cs)
- **Assembly**: `System.ServiceModel`
- **Type**: `System.ServiceModel.Dispatcher.AsyncMethodInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InvokeBegin | No | Yes |
| InvokeEnd | No | Yes |

### MethodInvokerWrapper (TaskMethodInvoker)
- **Wrapper**: [MethodInvokerWrapper.cs](MethodInvokerWrapper.cs)
- **Assembly**: `System.ServiceModel`
- **Type**: `System.ServiceModel.Dispatcher.TaskMethodInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InvokeAsync | No | Yes |
| InvokeEnd | No | Yes |

### DispatchBuilderWrapper
- **Wrapper**: [DispatchBuilderWrapper.cs](DispatchBuilderWrapper.cs)
- **Assembly**: `System.ServiceModel`
- **Type**: `System.ServiceModel.Description.DispatcherBuilder`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InitializeServiceHost | No | No |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Wcf3/Instrumentation.xml)

## Transaction Lifecycle

### Server-Side (Service) Instrumentation

1. **Transaction Creation**: WCF service method invocations create new transactions
2. **Outer Transaction Handling**: `WcfIgnoreOuterTransactionWrapper` instruments the `HostedHttpRequestAsyncResult` constructor to handle WCF-specific transaction lifecycle
3. **Method Execution**: `MethodInvokerWrapper` instruments the actual service method invocation:
   - **SyncMethodInvoker**: For synchronous service operations
   - **AsyncMethodInvoker**: For Begin/End async pattern operations
   - **TaskMethodInvoker**: For Task-based async operations
4. **Service Initialization**: `DispatchBuilderWrapper` instruments service host initialization

### Client-Side (Proxy) Instrumentation

1. **Channel Initialization**: `ChannelFactoryWrapper` instruments endpoint initialization
2. **Service Call**: `ServiceChannelProxyWrapper` instruments the proxy invocation, creating external segments for outbound WCF calls

## Distributed Tracing

The WCF instrumentation manages distributed tracing for cross-service communication:

- **Client-side**: Inserts distributed tracing headers into outbound SOAP messages
- **Server-side**: Accepts and processes distributed tracing headers from inbound SOAP messages
- **Header handling**: Uses WCF message headers for trace context propagation

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
