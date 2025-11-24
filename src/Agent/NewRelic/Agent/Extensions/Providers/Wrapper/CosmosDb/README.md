# New Relic .NET Agent Cosmos DB Instrumentation

## Overview
This wrapper adds datastore segments for Azure Cosmos DB client operations executed within an existing transaction. It captures database name, container (model), operation type, endpoint host/port, and query text (for query executions) without requiring user code changes.

## Instrumented Methods

### RequestInvokerHandlerWrapper
- Wrapper: [`RequestInvokerHandlerWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/CosmosDb/RequestInvokerHandlerWrapper.cs)
- Assembly: `Microsoft.Azure.Cosmos.Client`
- Type: `Microsoft.Azure.Cosmos.Handlers.RequestInvokerHandler`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| `SendAsync` | No | Yes |

### ExecuteItemQueryAsyncWrapper
- Wrapper: [`ExecuteItemQueryAsyncWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/CosmosDb/ExecuteItemQueryAsyncWrapper.cs)
- Assembly: `Microsoft.Azure.Cosmos.Client`
- Type: `Microsoft.Azure.Cosmos.CosmosQueryClientCore`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| `ExecuteItemQueryAsync` | No | Yes |
| `ExecuteItemQueryAsync` | No | Yes |

## Instrumentation XML
[`Instrumentation.xml`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/CosmosDb/Instrumentation.xml)

## Attributes Added
For each instrumented call (within an existing transaction) a datastore segment is created with:
- Database name (parsed from resource address)
- Model / container name (parsed from resource address)
- Operation (concatenation of operationType + resourceType)
- Endpoint host and port
- Query text (for `ExecuteItemQueryAsync` overloads when available)

## Distributed Tracing
These wrappers do not create transactions or process inbound distributed tracing headers. They run inside an existing transaction, contributing datastore span/segment data which participates in distributed traces automatically via the parent transaction.

## License
Copyright 2020 New Relic, Inc. All rights reserved.  
SPDX-License-Identifier: Apache-2.0
