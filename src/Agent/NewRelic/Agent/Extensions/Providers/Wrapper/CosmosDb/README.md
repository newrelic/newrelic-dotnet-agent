# New Relic .NET Agent Cosmos DB Instrumentation

## Overview
This wrapper adds datastore segments for Azure Cosmos DB client operations executed within an existing transaction. It captures database name, container (model), operation type, endpoint host/port, and query text (for query executions) without requiring user code changes.

## Instrumented Methods

### RequestInvokerHandlerWrapper
- Wrapper: [`RequestInvokerHandlerWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/CosmosDb/RequestInvokerHandlerWrapper.cs)
- Assembly: `Microsoft.Azure.Cosmos.Client`
- Type: `Microsoft.Azure.Cosmos.Handlers.RequestInvokerHandler`

| Method (name + parameters) | Creates Transaction | Requires Existing Transaction | Notes |
|----------------------------|---------------------|-------------------------------|-------|
| [`SendAsync(System.String, Microsoft.Azure.Documents.ResourceType, Microsoft.Azure.Documents.OperationType, Microsoft.Azure.Cosmos.RequestOptions, Microsoft.Azure.Cosmos.ContainerInternal, Microsoft.Azure.Cosmos.FeedRange, System.IO.Stream, System.Action\`1[Microsoft.Azure.Cosmos.RequestMessage], Microsoft.Azure.Cosmos.Tracing.ITrace, System.Threading.CancellationToken)`](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos/src/Handler/RequestInvokerHandler.cs) | No | Yes | Starts a Cosmos DB datastore segment. Parses resource address to extract database and container; operation name is concatenation of operationType + resourceType; captures endpoint host/port. |

### ExecuteItemQueryAsyncWrapper
- Wrapper: [`ExecuteItemQueryAsyncWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/CosmosDb/ExecuteItemQueryAsyncWrapper.cs)
- Assembly: `Microsoft.Azure.Cosmos.Client`
- Type: `Microsoft.Azure.Cosmos.CosmosQueryClientCore`

| Method (name + parameters) | Creates Transaction | Requires Existing Transaction | Notes |
|----------------------------|---------------------|-------------------------------|-------|
| [`ExecuteItemQueryAsync(System.String, Microsoft.Azure.Documents.ResourceType, Microsoft.Azure.Documents.OperationType, System.Guid, Microsoft.Azure.Cosmos.FeedRange, Microsoft.Azure.Cosmos.QueryRequestOptions, Microsoft.Azure.Cosmos.Query.Core.SqlQuerySpec, System.String, System.Boolean, System.Int32, Microsoft.Azure.Cosmos.Tracing.ITrace, System.Threading.CancellationToken)`](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos/src/Query/Core/CosmosQueryClientCore.cs) | No | Yes | Adds datastore segment with query text, database, container, operation (operationType + resourceType), endpoint host/port. |
| [`ExecuteItemQueryAsync(System.String, Microsoft.Azure.Documents.ResourceType, Microsoft.Azure.Documents.OperationType, Microsoft.Azure.Cosmos.FeedRange, Microsoft.Azure.Cosmos.QueryRequestOptions, Microsoft.Azure.Cosmos.Query.Core.AdditionalRequestHeaders, Microsoft.Azure.Cosmos.Query.Core.SqlQuerySpec, System.String, System.Int32, Microsoft.Azure.Cosmos.Tracing.ITrace, System.Threading.CancellationToken)`](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos/src/Query/Core/CosmosQueryClientCore.cs) | No | Yes | Alternate overload; same enrichment behavior capturing query text and connection details. |

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
