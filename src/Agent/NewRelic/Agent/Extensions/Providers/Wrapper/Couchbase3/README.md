# New Relic .NET Agent Couchbase3 Instrumentation

## Overview

The Couchbase3 instrumentation wrapper provides automatic monitoring for Couchbase .NET Client SDK 3.x operations executed within an existing transaction. It captures collection name, bucket name, operation type, and query text (for N1QL and Analytics queries) to create datastore segments for key-value, query, analytics, and search operations.

## Instrumented Methods

### Couchbase3CollectionWrapper
- **Wrapper**: [Couchbase3CollectionWrapper.cs](Couchbase3CollectionWrapper.cs)
- **Assembly**: `Couchbase.NetClient`
- **Type**: `Couchbase.KeyValue.CouchbaseCollection`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| [AppendAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [DecrementAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [ExistsAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [GetAllReplicasAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [GetAndLockAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [GetAndTouchAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [GetAnyReplicaAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [GetAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [IncrementAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [InsertAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [LookupInAllReplicasAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [LookupInAnyReplicaAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [LookupInAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [MutateInAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [PrependAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [RemoveAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [ReplaceAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [ScanAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [TouchAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [TouchWithCasAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [UnlockAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |
| [UpsertAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/CouchbaseCollection.cs) | No | Yes | 3.0.0.0 |

### Couchbase3QueryWrapper (Scope)
- **Wrapper**: [Couchbase3QueryWrapper.cs](Couchbase3QueryWrapper.cs)
- **Assembly**: `Couchbase.NetClient`
- **Type**: `Couchbase.KeyValue.Scope`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| [AnalyticsQueryAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/Scope.cs) | No | Yes | 3.0.0.0 |
| [QueryAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/Scope.cs) | No | Yes | 3.0.0.0 |
| [SearchAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/KeyValue/Scope.cs) | No | Yes | 3.0.0.0 |

### Couchbase3QueryWrapper (Cluster)
- **Wrapper**: [Couchbase3QueryWrapper.cs](Couchbase3QueryWrapper.cs)
- **Assembly**: `Couchbase.NetClient`
- **Type**: `Couchbase.Cluster`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| [AnalyticsQueryAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/Cluster.cs) | No | Yes | 3.0.0.0 |
| [QueryAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/Cluster.cs) | No | Yes | 3.0.0.0 |
| [SearchAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/Cluster.cs) | No | Yes | 3.0.0.0 |
| [SearchQueryAsync](https://github.com/couchbase/couchbase-net-client/blob/master/src/Couchbase/Cluster.cs) | No | Yes | 3.0.0.0 |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Couchbase3/Instrumentation.xml)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Set to "Couchbase"
- **Collection/Model name**: Retrieved from the `CouchbaseCollection.Name` property (for collection operations) or `Scope.Bucket.Name` (for scope/cluster operations)
- **Operation**: Method name (e.g., "GetAsync", "QueryAsync", "AnalyticsQueryAsync")
- **Query text**: Captured for `QueryAsync` and `AnalyticsQueryAsync` operations (first parameter); not captured for search operations

## Version Considerations

All instrumented methods specify a minimum version of 3.0.0.0. This wrapper targets Couchbase .NET Client SDK versions 3.x and later.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
