# New Relic .NET Agent Couchbase Instrumentation

## Overview

The Couchbase instrumentation wrapper provides automatic monitoring for Couchbase .NET Client operations executed within an existing transaction. It captures bucket name, operation type, and query text (for N1QL queries) to create datastore segments for key-value and query operations.

## Instrumented Methods

### CouchbaseDefaultWrapper / CouchbaseDefaultWrapperAsync
- **Wrapper**: [CouchbaseDefaultWrapper.cs](CouchbaseDefaultWrapper.cs), [CouchbaseDefaultWrapperAsync.cs](CouchbaseDefaultWrapperAsync.cs)
- **Assembly**: `Couchbase.NetClient`
- **Type**: `Couchbase.CouchbaseBucket`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| Append | No | Yes | 3.0.0.0 |
| AppendAsync | No | Yes | 3.0.0.0 |
| Decrement | No | Yes | 3.0.0.0 |
| DecrementAsync | No | Yes | 3.0.0.0 |
| Exists | No | Yes | 3.0.0.0 |
| ExistsAsync | No | Yes | 3.0.0.0 |
| Get | No | Yes | 3.0.0.0 |
| GetAndLock | No | Yes | 3.0.0.0 |
| GetAndLockAsync | No | Yes | 3.0.0.0 |
| GetAsync | No | Yes | 3.0.0.0 |
| GetAndTouch | No | Yes | 3.0.0.0 |
| GetAndTouchAsync | No | Yes | 3.0.0.0 |
| GetDocument | No | Yes | 3.0.0.0 |
| GetFromReplica | No | Yes | 3.0.0.0 |
| GetFromReplicaAsync | No | Yes | 3.0.0.0 |
| GetWithLock | No | Yes | 3.0.0.0 |
| GetWithLockAsync | No | Yes | 3.0.0.0 |
| Increment | No | Yes | 3.0.0.0 |
| IncrementAsync | No | Yes | 3.0.0.0 |
| Insert | No | Yes | 3.0.0.0 |
| InsertAsync | No | Yes | 3.0.0.0 |
| Invoke | No | Yes | 3.0.0.0 |
| InvokeAsync | No | Yes | 3.0.0.0 |
| Observe | No | Yes | 3.0.0.0 |
| ObserveAsync | No | Yes | 3.0.0.0 |
| Prepend | No | Yes | 3.0.0.0 |
| PrependAsync | No | Yes | 3.0.0.0 |
| Remove | No | Yes | 3.0.0.0 |
| RemoveAsync | No | Yes | 3.0.0.0 |
| Replace | No | Yes | 3.0.0.0 |
| ReplaceAsync | No | Yes | 3.0.0.0 |
| Touch | No | Yes | 3.0.0.0 |
| TouchAsync | No | Yes | 3.0.0.0 |
| Unlock | No | Yes | 3.0.0.0 |
| UnlockAsync | No | Yes | 3.0.0.0 |
| Upsert | No | Yes | 3.0.0.0 |
| UpsertAsync | No | Yes | 3.0.0.0 |

### CouchbaseQueryWrapper / CouchbaseQueryWrapperAsync
- **Wrapper**: [CouchbaseQueryWrapper.cs](CouchbaseQueryWrapper.cs), [CouchbaseQueryWrapperAsync.cs](CouchbaseQueryWrapperAsync.cs)
- **Assembly**: `Couchbase.NetClient`
- **Type**: `Couchbase.CouchbaseBucket`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| Query | No | Yes | 3.0.0.0 |
| QueryAsync | No | Yes | 3.0.0.0 |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Couchbase/Instrumentation.xml)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Set to "Couchbase"
- **Bucket name**: Retrieved from the `CouchbaseBucket.Name` property
- **Operation**: Method name (e.g., "Get", "Insert", "Query")
  - For multi-key operations (lists/dictionaries), "Multiple" is appended (e.g., "GetMultiple", "UpsertMultiple")
  - All Get* variants (GetAndLock, GetWithLock, etc.) are normalized to "Get"
- **Query text**: N1QL query statement (for Query/QueryAsync operations only)

## Operation Handling

### Batch Operations
Multi-key operations are instrumented as single datastore calls with "Multiple" appended to the operation name:
- `Get(IList<string>)` → "GetMultiple"
- `Remove(IList<string>)` → "RemoveMultiple"
- `Upsert(IDictionary<string, T>)` → "UpsertMultiple"

### Query Operations
N1QL queries executed via `Query` and `QueryAsync` methods capture the query statement text for detailed observability.

### Excluded Methods
The following single-key methods are intentionally not instrumented to avoid double instrumentation when called from instrumented batch operations:
- `Get(string)` - called by `Get(IList<string>)` overloads
- `Remove(string, ulong)` - called by `Remove(IList<string>)` overloads
- `Upsert(string, T)` - called by `Upsert(IDictionary<string, T>)` overloads

## Version Considerations

All instrumented methods specify a maximum version of 3.0.0.0 (exclusive). This wrapper targets Couchbase .NET Client SDK versions 2.x.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
