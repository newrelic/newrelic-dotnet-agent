# New Relic .NET Agent MongoDb Instrumentation

## Overview

The MongoDb instrumentation wrapper provides automatic monitoring for MongoDB .NET Driver (legacy driver, versions prior to 2.0) operations executed within an existing transaction. It creates datastore segments for collection operations (CRUD, aggregation, indexing), bulk write operations, cursor iterations, and database management operations.

## Instrumented Methods

### BulkWriteOperationExecuteWrapper
- **Wrapper**: [BulkWriteOperationExecuteWrapper.cs](BulkWriteOperationExecuteWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.BulkWriteOperation`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteHelper | No | Yes |
| Insert | No | Yes |

### MongoCollectionDefaultWrapper
- **Wrapper**: [MongoCollectionDefaultWrapper.cs](MongoCollectionDefaultWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoCollection`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Aggregate | No | Yes |
| CreateIndex | No | Yes |
| Drop | No | Yes |
| FindAndModify | No | Yes |
| FindAndRemove | No | Yes |
| GetIndexes | No | Yes |
| IndexExistsByName | No | Yes |
| InitializeOrderedBulkOperation | No | Yes |
| InitializeUnorderedBulkOperation | No | Yes |
| ParallelScanAs | No | Yes |
| Save | No | Yes |
| Update | No | Yes |
| Validate | No | Yes |

### MongoCollectionFindWrapper
- **Wrapper**: [MongoCollectionFindWrapper.cs](MongoCollectionFindWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoCollection`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| FindAs | No | Yes |
| FindOneAs | No | Yes |

### MongoCollectionInsertWrapper
- **Wrapper**: [MongoCollectionInsertWrapper.cs](MongoCollectionInsertWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoCollection`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InsertBatch | No | Yes |

### MongoCollectionRemoveWrapper
- **Wrapper**: [MongoCollectionRemoveWrapper.cs](MongoCollectionRemoveWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoCollection`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Remove | No | Yes |

### MongoCursorWrapper
- **Wrapper**: [MongoCursorWrapper.cs](MongoCursorWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoCursor\`1`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| GetEnumerator | No | Yes |

### MongoDatabaseDefaultWrapper
- **Wrapper**: [MongoDatabaseDefaultWrapper.cs](MongoDatabaseDefaultWrapper.cs)
- **Assembly**: `MongoDB.Driver`
- **Type**: `MongoDB.Driver.MongoDatabase`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CreateCollection | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/MongoDb/Instrumentation.xml)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Set to "MongoDB"
- **Collection name (model)**: Retrieved from `MongoCollection.Name` property
- **Database name**: Retrieved from `MongoDatabase.Name` property
- **Operation**: Determined from method name (e.g., "FindAs", "InsertBatch", "Update", "Aggregate")
- **Connection info**: Host and port from `MongoServer` connection

## Operation Mapping

The wrapper instruments methods at various levels of the MongoDB driver:

### Collection Operations
- **Find operations**: `FindAs`, `FindOneAs` (query operations)
- **Insert operations**: `InsertBatch` (bulk insert)
- **Update operations**: `Update`, `Save`, `FindAndModify`
- **Remove operations**: `Remove`, `FindAndRemove`
- **Aggregation**: `Aggregate`, `ParallelScanAs`
- **Index management**: `CreateIndex`, `GetIndexes`, `IndexExistsByName`
- **Collection management**: `Drop`, `Validate`

### Bulk Write Operations
- **Bulk write initialization**: `InitializeOrderedBulkOperation`, `InitializeUnorderedBulkOperation`
- **Bulk write execution**: `ExecuteHelper`, `Insert`

### Cursor Operations
- **Result iteration**: `GetEnumerator` (when iterating query results)

### Database Operations
- **Collection creation**: `CreateCollection`

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
