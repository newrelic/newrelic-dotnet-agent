# New Relic .NET Agent Memcached Instrumentation

## Overview

The Memcached instrumentation wrapper provides automatic monitoring for Memcached operations using the EnyimMemcachedCore client library within an existing transaction. It creates datastore segments for all key-value operations including store, get, mutate, concatenate, and remove operations.

## Instrumented Methods

### EnyimMemcachedCoreWrapper
- **Wrapper**: [EnyimMemcachedCoreWrapper.cs](EnyimMemcachedCoreWrapper.cs)
- **Assembly**: `EnyimMemcachedCore`
- **Type**: `Enyim.Caching.MemcachedClient`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| GetAsync | No | Yes |
| PerformConcatenate | No | Yes |
| PerformGet | No | Yes |
| PerformMutate | No | Yes |
| PerformMutateAsync | No | Yes |
| PerformStore | No | Yes |
| PerformStoreAsync | No | Yes |
| PerformTryGet | No | Yes |
| Remove | No | Yes |
| RemoveAsync | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Memcached/Instrumentation.xml)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Set to "Memcached"
- **Model**: Set to "cache"
- **Operation**: Determined from method name or first parameter:
  - `PerformStore`, `PerformStoreAsync`: Operation from first parameter (e.g., "Add", "Set", "Replace")
  - `PerformTryGet`, `PerformGet`, `GetAsync`: Operation set to "Get"
  - `PerformMutate`, `PerformMutateAsync`: Operation from first parameter (e.g., "Increment", "Decrement")
  - `PerformConcatenate`: Operation from first parameter ("Append" or "Prepend")
  - `Remove`, `RemoveAsync`: Operation set to "Remove"
- **Connection info**: Host and port determined from key using internal server selection logic
- **Key**: Cache key passed as parameter

## Operation Coverage

The wrapper instruments internal "Perform" methods that are called by public API methods:

### Store Operations
- **Add/AddAsync, Set/SetAsync, Replace/ReplaceAsync** → `PerformStore` / `PerformStoreAsync`
- **Store/StoreAsync** → `PerformStore` / `PerformStoreAsync`
- **Cas** → `PerformStore`

### Get Operations
- **Get** → TryGet → `PerformTryGet`
- **GetWithCas, GetWithCas\<T\>** → TryGetWithCas → `PerformTryGet`
- **Get\<T\>** → `PerformGet`
- **GetValueAsync** → `GetAsync`

### Mutate Operations
- **Increment, Decrement, CasMutate** → `PerformMutate`
- **TouchAsync** → `PerformMutateAsync`

### Concatenate Operations
- **Append, Prepend** → `PerformConcatenate`

### Remove Operations
- **Remove, RemoveAsync**: Instrumented directly

## Multi-Server Connection Info

The wrapper uses the cache key to determine which Memcached server will handle the operation in a multi-server environment. This allows accurate connection info (host and port) to be captured for each segment. Without the key, server information cannot be determined.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
