# New Relic .NET Agent MongoDB26 Instrumentation

## Overview

The MongoDB26 instrumentation wrapper provides automatic monitoring for MongoDB .NET Driver versions 2.0 and later operations executed within an existing transaction. It creates datastore segments for collection operations (CRUD, aggregation, bulk writes), cursor iterations, database management operations, index management, and LINQ query execution.

## Instrumented Methods

### MongoCollectionImplWrapper
- **Wrapper**: [MongoCollectionImplWrapper.cs](MongoCollectionImplWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoCollectionImpl\`1`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Aggregate | No | Yes |
| AggregateAsync | No | Yes |
| AggregateToCollection | No | Yes |
| AggregateToCollectionAsync | No | Yes |
| BulkWrite | No | Yes |
| BulkWriteAsync | No | Yes |
| Count | No | Yes |
| CountAsync | No | Yes |
| CountDocuments | No | Yes |
| CountDocumentsAsync | No | Yes |
| Distinct | No | Yes |
| DistinctAsync | No | Yes |
| EstimatedDocumentCount | No | Yes |
| EstimatedDocumentCountAsync | No | Yes |
| FindAsync | No | Yes |
| FindOneAndDelete | No | Yes |
| FindOneAndDeleteAsync | No | Yes |
| FindOneAndReplace | No | Yes |
| FindOneAndReplaceAsync | No | Yes |
| FindOneAndUpdate | No | Yes |
| FindOneAndUpdateAsync | No | Yes |
| FindSync | No | Yes |
| MapReduce | No | Yes |
| MapReduceAsync | No | Yes |
| Watch | No | Yes |
| WatchAsync | No | Yes |

### AsyncCursorWrapper (MongoDB.Driver.Core assembly, version < 3.0.0)
- **Wrapper**: [AsyncCursorWrapper.cs](AsyncCursorWrapper.cs)
- **Assembly**: `MongoDB.Driver.Core`
- **Type**: `MongoDB.Driver.Core.Operations.AsyncCursor\`1`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| GetNextBatch | No | Yes | 3.0.0 |
| GetNextBatchAsync | No | Yes | 3.0.0 |

### AsyncCursorWrapper (MongoDB.Driver assembly, version >= 3.0.0)
- **Wrapper**: [AsyncCursorWrapper.cs](AsyncCursorWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.Core.Operations.AsyncCursor\`1`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| GetNextBatch | No | Yes | 3.0.0 |
| GetNextBatchAsync | No | Yes | 3.0.0 |

### MongoDatabaseWrapper (MongoDatabaseImpl, version < 3.0.0)
- **Wrapper**: [MongoDatabaseWrapper.cs](MongoDatabaseWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoDatabaseImpl`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| Aggregate | No | Yes | 3.0.0 |
| AggregateAsync | No | Yes | 3.0.0 |
| AggregateToCollection | No | Yes | 3.0.0 |
| AggregateToCollectionAsync | No | Yes | 3.0.0 |
| CreateCollection | No | Yes | 3.0.0 |
| CreateCollectionAsync | No | Yes | 3.0.0 |
| CreateView | No | Yes | 3.0.0 |
| CreateViewAsync | No | Yes | 3.0.0 |
| DropCollection | No | Yes | 3.0.0 |
| DropCollectionAsync | No | Yes | 3.0.0 |
| ListCollectionNames | No | Yes | 3.0.0 |
| ListCollectionNamesAsync | No | Yes | 3.0.0 |
| ListCollections | No | Yes | 3.0.0 |
| ListCollectionsAsync | No | Yes | 3.0.0 |
| RenameCollection | No | Yes | 3.0.0 |
| RenameCollectionAsync | No | Yes | 3.0.0 |
| RunCommand | No | Yes | 3.0.0 |
| RunCommandAsync | No | Yes | 3.0.0 |
| Watch | No | Yes | 3.0.0 |
| WatchAsync | No | Yes | 3.0.0 |

### MongoDatabaseWrapper (MongoDatabase, version >= 3.0.0)
- **Wrapper**: [MongoDatabaseWrapper.cs](MongoDatabaseWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoDatabase`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| Aggregate | No | Yes | 3.0.0 |
| AggregateAsync | No | Yes | 3.0.0 |
| AggregateToCollection | No | Yes | 3.0.0 |
| AggregateToCollectionAsync | No | Yes | 3.0.0 |
| CreateCollection | No | Yes | 3.0.0 |
| CreateCollectionAsync | No | Yes | 3.0.0 |
| CreateView | No | Yes | 3.0.0 |
| CreateViewAsync | No | Yes | 3.0.0 |
| DropCollection | No | Yes | 3.0.0 |
| DropCollectionAsync | No | Yes | 3.0.0 |
| ListCollectionNames | No | Yes | 3.0.0 |
| ListCollectionNamesAsync | No | Yes | 3.0.0 |
| ListCollections | No | Yes | 3.0.0 |
| ListCollectionsAsync | No | Yes | 3.0.0 |
| RenameCollection | No | Yes | 3.0.0 |
| RenameCollectionAsync | No | Yes | 3.0.0 |
| RunCommand | No | Yes | 3.0.0 |
| RunCommandAsync | No | Yes | 3.0.0 |
| Watch | No | Yes | 3.0.0 |
| WatchAsync | No | Yes | 3.0.0 |

### MongoQueryProviderImplWrapper
- **Wrapper**: [MongoQueryProviderImplWrapper.cs](MongoQueryProviderImplWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.Linq.MongoQueryProviderImpl\`1`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteModel | No | Yes |
| ExecuteModelAsync | No | Yes |

### MongoIndexManagerBaseWrapper
- **Wrapper**: [MongoIndexManagerBaseWrapper.cs](MongoIndexManagerBaseWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoIndexManagerBase\`1`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CreateOne | No | Yes |
| CreateOneAsync | No | Yes |

### MongoIndexManagerWrapper
- **Wrapper**: [MongoIndexManagerWrapper.cs](MongoIndexManagerWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoCollectionImpl\`1+MongoIndexManager`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CreateMany | No | Yes |
| CreateManyAsync | No | Yes |
| DropAll | No | Yes |
| DropAllAsync | No | Yes |
| DropOne | No | Yes |
| DropOneAsync | No | Yes |
| List | No | Yes |
| ListAsync | No | Yes |

### MongoCollectionBaseWrapper
- **Wrapper**: [MongoCollectionBaseWrapper.cs](MongoCollectionBaseWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoCollectionBase\`1`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| DeleteMany | No | Yes |
| DeleteManyAsync | No | Yes |
| DeleteOne | No | Yes |
| DeleteOneAsync | No | Yes |
| InsertMany | No | Yes |
| InsertManyAsync | No | Yes |
| InsertOne | No | Yes |
| InsertOneAsync | No | Yes |
| ReplaceOne | No | Yes |
| ReplaceOneAsync | No | Yes |
| UpdateMany | No | Yes |
| UpdateManyAsync | No | Yes |
| UpdateOne | No | Yes |
| UpdateOneAsync | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/MongoDb26/Instrumentation.xml)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Set to "MongoDB"
- **Collection name (model)**: Retrieved from `CollectionNamespace` property
- **Database name**: Retrieved from `DatabaseNamespace` property
- **Operation**: Method name (e.g., "FindAsync", "InsertOne", "UpdateMany", "Aggregate")
- **Connection info**: Host and port from `MongoDatabase.Client.Settings.Server`

## Version Considerations

### Driver Version 3.0.0 Breaking Changes

MongoDB .NET Driver version 3.0.0 introduced architectural changes that affected instrumentation:

1. **AsyncCursor location**: Moved from `MongoDB.Driver.Core` assembly to `MongoDB.Driver` assembly
2. **Database implementation**: Changed from `MongoDatabaseImpl` to `MongoDatabase` public class

The instrumentation handles both versions:
- **Versions < 3.0.0**: Instruments `MongoDB.Driver.Core.Operations.AsyncCursor<T>` and `MongoDatabaseImpl`
- **Versions >= 3.0.0**: Instruments `MongoDB.Driver.Core.Operations.AsyncCursor<T>` (in MongoDB.Driver assembly) and `MongoDatabase`

## Operation Categories

### Collection Operations (MongoCollectionImpl)
- **CRUD**: FindSync/FindAsync, InsertOne/Many, UpdateOne/Many, DeleteOne/Many, ReplaceOne
- **Aggregation**: Aggregate, AggregateToCollection, MapReduce, Distinct
- **Counting**: Count, CountDocuments, EstimatedDocumentCount
- **Atomic operations**: FindOneAndDelete, FindOneAndReplace, FindOneAndUpdate
- **Bulk operations**: BulkWrite
- **Change streams**: Watch

### Database Operations (MongoDatabase/MongoDatabaseImpl)
- **Collection management**: CreateCollection, DropCollection, RenameCollection, ListCollections, ListCollectionNames
- **View management**: CreateView
- **Aggregation**: Aggregate, AggregateToCollection
- **Command execution**: RunCommand
- **Change streams**: Watch

### Index Operations
- **Index creation**: CreateOne, CreateMany (MongoIndexManager)
- **Index removal**: DropOne, DropAll
- **Index listing**: List

### LINQ Query Operations (MongoQueryProviderImpl)
- **Query execution**: ExecuteModel, ExecuteModelAsync (executes LINQ queries translated to MongoDB operations)

### Cursor Operations (AsyncCursor)
- **Batch retrieval**: GetNextBatch, GetNextBatchAsync (fetches next batch of results from server)

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
