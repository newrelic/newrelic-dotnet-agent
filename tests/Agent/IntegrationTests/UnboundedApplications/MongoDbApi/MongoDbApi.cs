// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared;

namespace MongoDbApi
{
    public class MongoDbApi
    {

        const string CollectionName = "myCollection";

        private readonly IMongoDatabase _db;
        private readonly string _defaultCollectionName;
        private readonly IMongoClient _client;

        public MongoDbApi()
        {
            _client = new MongoClient(new MongoUrl(MongoDbConfiguration.MongoDb26ConnectionString));
        }

        public MongoDbApi(string databaseName = "myDb")
        {
            _client = new MongoClient(new MongoUrl(MongoDbConfiguration.MongoDb26ConnectionString));
            _db = _client.GetDatabase(databaseName);
            _defaultCollectionName = CollectionName;
        }

        #region Drop

        public void DropDatabase(string databaseName)
        {
            _client.DropDatabase(databaseName);
        }

        #endregion Drop


        #region Insert

        public void InsertOne()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            collection.InsertOne(document);
        }

        public async Task InsertOneAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred 'Async' Flintstone" };
            await collection.InsertOneAsync(document);
        }

        public void InsertMany()
        {
            var collection = GetAddCollection();
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Pebbles Flintstone" };
            collection.InsertMany(new List<CustomMongoDbEntity>() { doc1, doc2 });
        }

        public async Task InsertManyAsync()
        {
            var collection = GetAddCollection();
            var document1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma 'Async' Flintstone" };
            var document2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Pebbles 'Async' Flintstone" };
            await collection.InsertManyAsync(new List<CustomMongoDbEntity>() { document1, document2 });
        }

        #endregion

        #region Replace
        public void ReplaceOne()
        {
            var collection = GetAddCollection();
            collection.InsertOne(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Mr. Slate" });
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Mr.Slate");
            collection.ReplaceOne(filter, new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" });
        }

        public async Task ReplaceOneAsync()
        {
            var collection = GetAddCollection();
            collection.InsertOne(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Mr. Slate" });
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Mr.Slate");
            await collection.ReplaceOneAsync(filter, new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" });
        }
        #endregion

        #region Update

        public UpdateResult UpdateOne()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Dino Flintstone" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Dino Flintstone");
            var update = Builders<CustomMongoDbEntity>.Update.Set("Name", "Dinosaur Flintstone");
            var result = collection.UpdateOne(filter, update);
            return result;
        }

        public async Task<UpdateResult> UpdateOneAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Dino Flintstone" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Dino 'Async' Flintstone");
            var update = Builders<CustomMongoDbEntity>.Update.Set("Name", "Dinosaur 'Async' Flintstone");
            var result = await collection.UpdateOneAsync(filter, update);
            return result;
        }

        public UpdateResult UpdateMany()
        {
            var collection = GetAddCollection();
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Pebbles Flintstone" };
            collection.InsertMany(new List<CustomMongoDbEntity>() { doc1, doc2 });

            var filter = Builders<CustomMongoDbEntity>.Filter.In("Name", new List<string> { "Willma Flintstone", "Pebbles Flintstone" });
            var update = Builders<CustomMongoDbEntity>.Update.Set("familyName", "Flintstone");
            var result = collection.UpdateMany(filter, update);
            return result;
        }

        public async Task<UpdateResult> UpdateManyAsync()
        {
            var collection = GetAddCollection();
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Pebbles Flintstone" };
            collection.InsertMany(new List<CustomMongoDbEntity>() { doc1, doc2 });

            var filter = Builders<CustomMongoDbEntity>.Filter.In("Name", new List<string> { "Willma Flintstone", "Pebbles Flintstone" });
            var update = Builders<CustomMongoDbEntity>.Update.Set("familyName", "Flintstone 'Async'");
            var result = await collection.UpdateManyAsync(filter, update);
            return result;
        }

        #endregion

        #region Delete

        public DeleteResult DeleteOne()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Barney Rubble" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Barney Rubble");
            var result = collection.DeleteOne(filter);
            return result;
        }

        public async Task<DeleteResult> DeleteOneAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Barney 'Async' Rubble" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Barney 'Async' Rubble");
            var result = await collection.DeleteOneAsync(filter);
            return result;
        }

        public DeleteResult DeleteMany()
        {
            var collection = GetAddCollection();
            var document1 = (new CustomMongoDbEntity { Id = new ObjectId(), Name = "Betty Rubble" });
            var document2 = (new CustomMongoDbEntity { Id = new ObjectId(), Name = "BamBam Rubble" });
            collection.InsertMany(new List<CustomMongoDbEntity>() { document1, document2 });

            var filter = Builders<CustomMongoDbEntity>.Filter.In("Name", new List<string> { "Betty Rubble", "BamBam Rubble" });
            var result = collection.DeleteMany(filter);
            return result;

        }

        public async Task<DeleteResult> DeleteManyAsync()
        {
            var collection = GetAddCollection();
            var document1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Betty 'Async' Rubble" };
            var document2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "BamBam 'Async' Rubble" };
            collection.InsertMany(new List<CustomMongoDbEntity>() { document1, document2 });

            var filter = Builders<CustomMongoDbEntity>.Filter.In("Name", new List<string> { "Betty 'Async' Rubble", "BamBam 'Async' Rubble" });
            var result = await collection.DeleteManyAsync(filter);
            return result;
        }

        #endregion

        #region Find

        public IAsyncCursor<CustomMongoDbEntity> FindSync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Mr. Slate" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Mr.Slate");
            var cursor = collection.FindSync(filter);
            return cursor;
        }

        public async Task<IAsyncCursor<CustomMongoDbEntity>> FindAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Mr. Slate" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Mr.Slate");
            var cursor = await collection.FindAsync(filter);
            return cursor;
        }

        public CustomMongoDbEntity FindOneAndDelete()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "The Great Gazoo" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "The Great Gazoo");
            var entity = collection.FindOneAndDelete(filter);
            return entity;
        }

        public async Task<CustomMongoDbEntity> FindOneAndDeleteAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "The Great 'Async' Gazoo" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "The Great 'Async' Gazoo");
            var entity = await collection.FindOneAndDeleteAsync(filter);
            return entity;
        }

        public CustomMongoDbEntity FindOneAndReplace()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Joe Rockhead" };
            collection.InsertOne(document);

            var replaceDoc = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Joe Rockhead's Doppelganger" };
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Joe Rockhead");
            var entity = collection.FindOneAndReplace(filter, replaceDoc);
            return entity;
        }

        public async Task<CustomMongoDbEntity> FindOneAndReplaceAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Joe 'Async' Rockhead" };
            collection.InsertOne(document);

            var replaceDoc = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Joe 'Async' Rockhead's Doppelganger" };
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Joe 'Async' Rockhead");
            var entity = await collection.FindOneAndReplaceAsync(filter, replaceDoc);
            return entity;
        }

        public CustomMongoDbEntity FindOneAndUpdate()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Roxy Rubble" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Roxy Rubble");
            var update = Builders<CustomMongoDbEntity>.Update.Set("familyName", "Rubble");
            var entity = collection.FindOneAndUpdate(filter, update);
            return entity;
        }

        public async Task<CustomMongoDbEntity> FindOneAndUpdateAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Roxy 'Async' Rubble" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Roxy 'Async' Rubble");
            var update = Builders<CustomMongoDbEntity>.Update.Set("familyName", "'Async' Rubble");
            var entity = await collection.FindOneAndUpdateAsync<CustomMongoDbEntity>(filter, update);
            return entity;
        }

        #endregion

        #region Other

        public BulkWriteResult BulkWrite()
        {
            var collection = GetAddCollection();
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma Flintstone" };

            var result = collection.BulkWrite(new WriteModel<CustomMongoDbEntity>[] {
                new InsertOneModel<CustomMongoDbEntity>(doc1),
                new InsertOneModel<CustomMongoDbEntity>(doc2)
            });
            return result;
        }

        public async Task<BulkWriteResult> BulkWriteAsync()
        {
            var collection = GetAddCollection();
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred 'Async' Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma 'Async' Flintstone" };

            var result = await collection.BulkWriteAsync(new WriteModel<CustomMongoDbEntity>[] {
                new InsertOneModel<CustomMongoDbEntity>(doc1),
                new InsertOneModel<CustomMongoDbEntity>(doc2)
            });
            return result;
        }

        public IAsyncCursor<CustomMongoDbEntity> Aggregate()
        {
            var collection = GetAddCollection();

            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            collection.InsertOne(document);

            var match = new BsonDocument
            {
                {
                    "$match",
                    new BsonDocument
                    {
                        { "Name", "Fred Flintstone" }
                    }
                }
            };

            var pipeline = new[] { match };
            var result = collection.Aggregate<CustomMongoDbEntity>(pipeline);
            return result;
        }

        public long Count()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            collection.InsertOne(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");

            return collection.Count(filter);
        }

        public async Task<long> CountAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            collection.InsertOne(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");

            return await collection.CountAsync(filter);
        }

        public IAsyncCursor<string> Distinct()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            collection.InsertOne(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");

            return collection.Distinct<string>("Name", filter);
        }

        public async Task<IAsyncCursor<string>> DistinctAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            collection.InsertOne(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");

            return await collection.DistinctAsync<string>("Name", filter);
        }

        public IAsyncCursor<BsonDocument> MapReduce()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            collection.InsertOne(document);

            var mapJs = @"function mapF() {
							emit(this.name, 1);
						};";

            var reduceJs = @"function reduceF(key, values) {
							var sum = Array.sum(values);
							return new NumberLong(sum);
						};";

            BsonJavaScript map = new BsonJavaScript(mapJs);
            BsonJavaScript reduce = new BsonJavaScript(reduceJs);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");

            MapReduceOptions<CustomMongoDbEntity, BsonDocument> options = new MapReduceOptions<CustomMongoDbEntity, BsonDocument>
            {
                Filter = filter,
                OutputOptions = MapReduceOutputOptions.Inline
            };

            return collection.MapReduce(map, reduce, options);
        }

        public async Task<IAsyncCursor<BsonDocument>> MapReduceAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            collection.InsertOne(document);

            var mapJs = @"function mapF() {
							emit(this.name, 1);
						};";

            var reduceJs = @"function reduceF(key, values) {
							var sum = Array.sum(values);
							return new NumberLong(sum);
						};";

            BsonJavaScript map = new BsonJavaScript(mapJs);
            BsonJavaScript reduce = new BsonJavaScript(reduceJs);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");

            MapReduceOptions<CustomMongoDbEntity, BsonDocument> options = new MapReduceOptions<CustomMongoDbEntity, BsonDocument>
            {
                Filter = filter,
                OutputOptions = MapReduceOutputOptions.Inline
            };

            return await collection.MapReduceAsync(map, reduce, options);
        }

        //This call will throw exception because the Watch() method only work with MongoDb replica sets, but it is fine as long as the method is executed. 
        public string Watch()
        {
            try
            {
                var collection = GetAddCollection();
                var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
                collection.InsertOne(document);
                collection.Watch();

                return "Ok";
            }
            catch (MongoCommandException)
            {
                return "Got exception but it is ok!";
            }
        }

        //This call will throw exception because the Watch() method only work with MongoDb replica sets, but it is fine as long as the method is executed.
        public async Task<string> WatchAsync()
        {
            try
            {
                var collection = GetAddCollection();
                var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
                collection.InsertOne(document);
                await collection.WatchAsync();

                return "Ok";

            }
            catch (MongoCommandException)
            {
                return "Got exception but it is ok!";
            }
        }

        #endregion

        #region Database

        public string CreateCollection()
        {
            var collectionName = "createTestCollection";
            _db.CreateCollection(collectionName);
            var collection = _db.GetCollection<BsonDocument>(collectionName);
            _db.DropCollection(collectionName);
            return collection.CollectionNamespace.CollectionName;
        }

        public async Task<string> CreateCollectionAsync()
        {
            var collectionName = "createTestCollectionAsync";
            await _db.CreateCollectionAsync(collectionName);
            var collection = _db.GetCollection<BsonDocument>(collectionName);
            await _db.DropCollectionAsync(collectionName);
            return collection.CollectionNamespace.CollectionName;
        }

        public string DropCollection()
        {
            var collectionName = "dropTestCollection";
            _db.CreateCollection(collectionName);
            var collection = _db.GetCollection<BsonDocument>(collectionName);
            _db.DropCollection(collectionName);
            return collection.CollectionNamespace.CollectionName;
        }

        public async Task<string> DropCollectionAsync()
        {
            var collectionName = "dropTestCollectionAsync";
            _db.CreateCollection(collectionName);
            var collection = _db.GetCollection<BsonDocument>(collectionName);
            await _db.DropCollectionAsync(collectionName);
            return collection.CollectionNamespace.CollectionName;
        }

        public int ListCollections()
        {
            var collections = _db.ListCollections().ToList();
            return collections.Count();
        }

        public async Task<int> ListCollectionsAsync()
        {
            var cursor = await _db.ListCollectionsAsync();
            var collections = cursor.ToList();
            return collections.Count;
        }

        public string RenameCollection()
        {
            var collectionName = "NamedCollection";
            var newName = "RenamedCollection";

            var collection = _db.GetCollection<CustomMongoDbEntity>(collectionName);
            EnsureCollectionExists(collection);

            _db.RenameCollection(collectionName, newName);
            var newCollection = _db.GetCollection<BsonDocument>(newName);
            return newCollection.CollectionNamespace.CollectionName;
        }

        public async Task<string> RenameCollectionAsync()
        {
            var collectionName = "NamedCollectionAsync";
            var newName = "RenamedCollectionAsync";

            var collection = _db.GetCollection<CustomMongoDbEntity>(collectionName);
            EnsureCollectionExists(collection);

            await _db.RenameCollectionAsync(collectionName, newName);
            var newCollection = _db.GetCollection<BsonDocument>(newName);
            return newCollection.CollectionNamespace.CollectionName;
        }

        public string RunCommand()
        {
            var command = new BsonDocumentCommand<BsonDocument>(new BsonDocument { { "dbStats", 1 }, { "scale", 1 } });
            var result = _db.RunCommand<BsonDocument>(command);
            return result.ToString();
        }

        public async Task<string> RunCommandAsync()
        {
            var command = new BsonDocumentCommand<BsonDocument>(new BsonDocument { { "dbStats", 1 }, { "scale", 1 } });
            var result = await _db.RunCommandAsync<BsonDocument>(command);
            return result.ToString();
        }

        #endregion

        #region IndexManager

        public int CreateOne()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "" };
            collection.InsertOne(document);
            collection.Indexes.CreateOne(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name));
            return 1;
        }

        public async Task<int> CreateOneAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "" };
            collection.InsertOne(document);
            await collection.Indexes.CreateOneAsync(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name));
            return 1;
        }

        public int CreateMany()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "" };
            collection.InsertOne(document);

            var result = collection.Indexes.CreateMany(new[] { new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)) });
            return result.Count();
        }

        public async Task<int> CreateManyAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "" };
            collection.InsertOne(document);

            var result = await collection.Indexes.CreateManyAsync(new[] { new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)) });
            return result.Count();
        }

        public void DropAll()
        {
            var collection = GetAddCollection();
            collection.Indexes.DropAll();
        }

        public async Task DropAllAsync()
        {
            var collection = GetAddCollection();
            await collection.Indexes.DropAllAsync();
        }

        public void DropOne()
        {
            var collection = GetAddCollection();
            collection.Indexes.CreateMany(new[] { new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)) });
            collection.Indexes.DropOne("Name_1");
        }

        public async Task DropOneAsync()
        {
            var collection = GetAddCollection();
            collection.Indexes.CreateMany(new[] { new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)) });
            await collection.Indexes.DropOneAsync("Name_1");
        }

        public int List()
        {
            var collection = GetAddCollection();
            var cursor = collection.Indexes.List();
            var result = cursor.ToList();
            return result.Count();
        }

        public async Task<int> ListAsync()
        {
            var collection = GetAddCollection();
            var cursor = await collection.Indexes.ListAsync();
            var result = cursor.ToList();
            return result.Count();
        }

        #endregion

        #region Linq

        public int ExecuteModel()
        {
            var collection = GetAddCollection();
            var result = collection.AsQueryable().Where(w => w.Name == "Fred Flintstone");
            return result.Count();
        }

        public async Task<int> ExecuteModelAsync()
        {
            var collection = GetAddCollection();
            var cursor = await collection.AsQueryable().Where(w => w.Name == "Fred Flintstone").ToCursorAsync();
            var result = cursor.ToList().Count();
            return result;
        }

        #endregion

        #region AsyncCursor

        public int GetNextBatch()
        {
            var collection = GetAddCollection();

            collection.InsertMany(new[]
            {
                new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" },
                new CustomMongoDbEntity { Id = new ObjectId(), Name = "Alan Flintstone" }
            });

            var filter = Builders<CustomMongoDbEntity>.Filter.Exists("Name");

            var cursor = collection.FindSync(filter, new FindOptions<CustomMongoDbEntity, CustomMongoDbEntity>()
            {
                BatchSize = 1
            });

            var result = cursor.ToList();
            return result.Count;
        }

        public async Task<int> GetNextBatchAsync()
        {
            var collection = GetAddCollection();

            collection.InsertMany(new[]
            {
                new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" },
                new CustomMongoDbEntity { Id = new ObjectId(), Name = "Alan Flintstone" }
            });

            var filter = Builders<CustomMongoDbEntity>.Filter.Exists("Name");

            var cursor = await collection.FindAsync(filter, new FindOptions<CustomMongoDbEntity, CustomMongoDbEntity>()
            {
                BatchSize = 1
            });

            var result = await cursor.ToListAsync();
            return result.Count;
        }

        #endregion

        #region Helpers

        private IMongoCollection<CustomMongoDbEntity> GetAddCollection(string collectionName = null)
        {
            collectionName = collectionName ?? _defaultCollectionName;

            var collection = _db.GetCollection<CustomMongoDbEntity>(collectionName);

            if (collection == null)
            {
                _db.CreateCollection(collectionName);
                collection = _db.GetCollection<CustomMongoDbEntity>(collectionName);
            }

            return collection;
        }

        private void EnsureCollectionExists(IMongoCollection<CustomMongoDbEntity> collection)
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "collection_exists_document" };
            collection.InsertOne(document);
        }

        private void DropCollection(string collectionName)
        {
            _db.DropCollection(collectionName);
        }

        #endregion
    }
}
