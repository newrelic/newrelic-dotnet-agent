// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Several methods exercised here do not exist in MongoDB.Driver version 2.3 which is the oldest we support on .NET Framework. It is bound to the net462 TFM in MultiFunctionApplicationHelpers.csproj
#if NET462
#define MONGODRIVER2_3
#endif

// Some methods exercised here do not exist in MongoDB.Driver version 2.8.1 which is the oldest we support on .NET Core. It is bound to the net6.0 TFM in MultiFunctionApplicationHelpers.csproj
#if NET6_0
#define MONGODRIVER2_8_1
#endif

using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using System.Runtime.CompilerServices;
using NewRelic.Api.Agent;
using System;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MongoDB
{
    [Library]
    public class MongoDBDriverExerciser
    {
        const string CollectionName = "myCollection";

        private readonly string _dbName = Guid.NewGuid().ToString();
        private readonly string _defaultCollectionName = CollectionName;

        private IMongoClient _client;
        private IMongoDatabase _db;
        private IMongoCollection<CustomMongoDbEntity> _collection;
        private string _mongoUrl;

        public IMongoClient Client => _client ??= new MongoClient(new MongoUrl(_mongoUrl));
        public IMongoDatabase Db => _db ??= Client.GetDatabase(_dbName);
        public IMongoCollection<CustomMongoDbEntity> Collection => _collection ??= GetAddCollection();

        // This method should be called by users of this exerciser before calling any other methods
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void SetMongoUrl(string mongoUrl)
        {
            _mongoUrl = mongoUrl;
        }


#region Drop

        public void DropDatabase(string databaseName)
        {
            Client.DropDatabase(databaseName);
        }

#endregion Drop

#region Insert

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void InsertOne()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            Collection.InsertOne(document);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task InsertOneAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred 'Async' Flintstone" };
            await Collection.InsertOneAsync(document);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void InsertMany()
        {
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Pebbles Flintstone" };
            Collection.InsertMany(new List<CustomMongoDbEntity>() { doc1, doc2 });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task InsertManyAsync()
        {
            var document1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma 'Async' Flintstone" };
            var document2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Pebbles 'Async' Flintstone" };
            await Collection.InsertManyAsync(new List<CustomMongoDbEntity>() { document1, document2 });
        }

#endregion

#region Replace

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ReplaceOne()
        {
            Collection.InsertOne(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Mr. Slate" });
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Mr.Slate");
            Collection.ReplaceOne(filter, new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task ReplaceOneAsync()
        {
            await Collection.InsertOneAsync(new CustomMongoDbEntity { Id = new ObjectId(), Name = "Mr. Slate" });
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Mr.Slate");
            await Collection.ReplaceOneAsync(filter, new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" });
        }
#endregion

#region Update

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public UpdateResult UpdateOne()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Dino Flintstone" };
            Collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Dino Flintstone");
            var update = Builders<CustomMongoDbEntity>.Update.Set("Name", "Dinosaur Flintstone");
            var result = Collection.UpdateOne(filter, update);
            return result;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<UpdateResult> UpdateOneAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Dino Flintstone" };
            await Collection.InsertOneAsync(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Dino 'Async' Flintstone");
            var update = Builders<CustomMongoDbEntity>.Update.Set("Name", "Dinosaur 'Async' Flintstone");
            var result = await Collection.UpdateOneAsync(filter, update);
            return result;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public UpdateResult UpdateMany()
        {
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Pebbles Flintstone" };
            Collection.InsertMany(new List<CustomMongoDbEntity>() { doc1, doc2 });

            var filter = Builders<CustomMongoDbEntity>.Filter.In("Name", new List<string> { "Willma Flintstone", "Pebbles Flintstone" });
            var update = Builders<CustomMongoDbEntity>.Update.Set("familyName", "Flintstone");
            var result = Collection.UpdateMany(filter, update);
            return result;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<UpdateResult> UpdateManyAsync()
        {
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Pebbles Flintstone" };
            await Collection.InsertManyAsync(new List<CustomMongoDbEntity>() { doc1, doc2 });

            var filter = Builders<CustomMongoDbEntity>.Filter.In("Name", new List<string> { "Willma Flintstone", "Pebbles Flintstone" });
            var update = Builders<CustomMongoDbEntity>.Update.Set("familyName", "Flintstone 'Async'");
            var result = await Collection.UpdateManyAsync(filter, update);
            return result;
        }

#endregion

#region Delete

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public DeleteResult DeleteOne()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Barney Rubble" };
            Collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Barney Rubble");
            var result = Collection.DeleteOne(filter);
            return result;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<DeleteResult> DeleteOneAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Barney 'Async' Rubble" };
            await Collection.InsertOneAsync(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Barney 'Async' Rubble");
            var result = await Collection.DeleteOneAsync(filter);
            return result;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public DeleteResult DeleteMany()
        {
            var document1 = (new CustomMongoDbEntity { Id = new ObjectId(), Name = "Betty Rubble" });
            var document2 = (new CustomMongoDbEntity { Id = new ObjectId(), Name = "BamBam Rubble" });
            Collection.InsertMany(new List<CustomMongoDbEntity>() { document1, document2 });

            var filter = Builders<CustomMongoDbEntity>.Filter.In("Name", new List<string> { "Betty Rubble", "BamBam Rubble" });
            var result = Collection.DeleteMany(filter);
            return result;

        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<DeleteResult> DeleteManyAsync()
        {
            var document1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Betty 'Async' Rubble" };
            var document2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "BamBam 'Async' Rubble" };
            await Collection.InsertManyAsync(new List<CustomMongoDbEntity>() { document1, document2 });

            var filter = Builders<CustomMongoDbEntity>.Filter.In("Name", new List<string> { "Betty 'Async' Rubble", "BamBam 'Async' Rubble" });
            var result = await Collection.DeleteManyAsync(filter);
            return result;
        }

#endregion

#region Find

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public IAsyncCursor<CustomMongoDbEntity> FindSync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Mr. Slate" };
            Collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq(x => x.Id, document.Id);
            var cursor = Collection.FindSync(filter);
            return cursor;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<IAsyncCursor<CustomMongoDbEntity>> FindAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Mr. Slate" };
            await Collection.InsertOneAsync(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq(x => x.Id, document.Id);
            var cursor = await Collection.FindAsync(filter);
            return cursor;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public CustomMongoDbEntity FindOneAndDelete()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "The Great Gazoo" };
            Collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq(x => x.Id, document.Id);
            var entity = Collection.FindOneAndDelete(filter);
            return entity;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<CustomMongoDbEntity> FindOneAndDeleteAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "The Great 'Async' Gazoo" };
            await Collection.InsertOneAsync(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq(x => x.Id, document.Id);
            var entity = await Collection.FindOneAndDeleteAsync(filter);
            return entity;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public CustomMongoDbEntity FindOneAndReplace()
        {

            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Joe Rockhead" };
            Collection.InsertOne(document);

            var replaceDoc = new CustomMongoDbEntity { Id = document.Id, Name = "Joe Rockhead's Doppelganger" };
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq(x => x.Id, document.Id);

            var option = new FindOneAndReplaceOptions<CustomMongoDbEntity> { IsUpsert = true };

            Collection.FindOneAndReplace<CustomMongoDbEntity>(filter, replaceDoc, option);

            var entity = Collection.FindOneAndReplace(filter, replaceDoc, option);
            return entity;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<CustomMongoDbEntity> FindOneAndReplaceAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Joe 'Async' Rockhead" };
            await Collection.InsertOneAsync(document);

            var replaceDoc = new CustomMongoDbEntity { Id = document.Id, Name = "Joe 'Async' Rockhead's Doppelganger" };
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq(x => x.Id, document.Id);

            var option = new FindOneAndReplaceOptions<CustomMongoDbEntity> { IsUpsert = true };

            var entity = await Collection.FindOneAndReplaceAsync(filter, replaceDoc, option);
            return entity;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public CustomMongoDbEntity FindOneAndUpdate()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Roxy Rubble" };
            Collection.InsertOne(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq(x => x.Id, document.Id);
            var update = Builders<CustomMongoDbEntity>.Update.Set("Name", "Rubble");

            var option = new FindOneAndUpdateOptions<CustomMongoDbEntity> { IsUpsert = true };

            var entity = Collection.FindOneAndUpdate(filter, update, option);
            return entity;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<CustomMongoDbEntity> FindOneAndUpdateAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Roxy 'Async' Rubble" };
            await Collection.InsertOneAsync(document);

            var filter = Builders<CustomMongoDbEntity>.Filter.Eq(x => x.Id, document.Id);
            var update = Builders<CustomMongoDbEntity>.Update.Set("Name", "'Async' Rubble");

            var option = new FindOneAndUpdateOptions<CustomMongoDbEntity> { ReturnDocument = ReturnDocument.Before };

            var entity = await Collection.FindOneAndUpdateAsync(filter, update, option);
            return entity;
        }

#endregion

#region Other

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public BulkWriteResult BulkWrite()
        {
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma Flintstone" };

            var result = Collection.BulkWrite(new WriteModel<CustomMongoDbEntity>[] {
                new InsertOneModel<CustomMongoDbEntity>(doc1),
                new InsertOneModel<CustomMongoDbEntity>(doc2)
            });
            return result;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<BulkWriteResult> BulkWriteAsync()
        {
            var doc1 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred 'Async' Flintstone" };
            var doc2 = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Willma 'Async' Flintstone" };

            var result = await Collection.BulkWriteAsync(new WriteModel<CustomMongoDbEntity>[] {
                new InsertOneModel<CustomMongoDbEntity>(doc1),
                new InsertOneModel<CustomMongoDbEntity>(doc2)
            });
            return result;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public IAsyncCursor<CustomMongoDbEntity> Aggregate()
        {

            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            Collection.InsertOne(document);

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
            var result = Collection.Aggregate<CustomMongoDbEntity>(pipeline);
            return result;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<IAsyncCursor<CustomMongoDbEntity>> AggregateAsync()
        {

            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            await Collection.InsertOneAsync(document);

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
            var result = Collection.AggregateAsync<CustomMongoDbEntity>(pipeline);
            return await result;
        }

#if !MONGODRIVER2_3 && !MONGODRIVER2_8_1
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void AggregateToCollection()
        {

            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            Collection.InsertOne(document);

            var matchStage = new BsonDocument
            {
                {
                    "$match",
                    new BsonDocument
                    {
                        { "Name", "Fred Flintstone" }
                    }
                }
            };

            var outStage = new BsonDocument
            {
                {
                    "$out",
                    new BsonDocument
                    {
                        { "db", _dbName },
                        { "coll", _defaultCollectionName }
                    }
                }
            };

            var pipeline = new[] { matchStage, outStage };
            Collection.AggregateToCollection<CustomMongoDbEntity>(pipeline);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task AggregateToCollectionAsync()
        {

            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            await Collection.InsertOneAsync(document);

            var matchStage = new BsonDocument
            {
                {
                    "$match",
                    new BsonDocument
                    {
                        { "Name", "Fred Flintstone" }
                    }
                }
            };

            var outStage = new BsonDocument
            {
                {
                    "$out",
                    new BsonDocument
                    {
                        { "db", _dbName },
                        { "coll", _defaultCollectionName }
                    }
                }
            };

            var pipeline = new[] { matchStage, outStage };
            await Collection.AggregateToCollectionAsync<CustomMongoDbEntity>(pipeline);
        }
#endif

        // The Count and CountAsync methods are replaced and marked Obsolete in newer versions of the driver.
        // We are continuing to test these methods until they are removed because of how the instrumentation
        // for MongoDB was implemented.
#if !MONGODRIVER2_3
#pragma warning disable CS0618 // Type or member is obsolete
#endif
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public long Count()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            Collection.InsertOne(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");
            return Collection.Count(filter);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<long> CountAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            await Collection.InsertOneAsync(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");
            return await Collection.CountAsync(filter);
        }
#if !MONGODRIVER2_3
        // Restoring Obsolete warnings so that we can more easily identify instrumentation gaps
#pragma warning restore CS0618 // Type or member is obsolete
#endif

        // CountDocuments{Async} did not exist in driver version 2.3 which is bound to net462 in MultiFunctionApplicationHelpers.csproj
#if !MONGODRIVER2_3
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public long CountDocuments()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            Collection.InsertOne(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");
            return Collection.CountDocuments(filter);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<long> CountDocumentsAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            await Collection.InsertOneAsync(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");
            return await Collection.CountDocumentsAsync(filter);
        }
#endif

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public IAsyncCursor<string> Distinct()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            Collection.InsertOne(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");

            return Collection.Distinct<string>("Name", filter);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<IAsyncCursor<string>> DistinctAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            await Collection.InsertOneAsync(document);
            var filter = Builders<CustomMongoDbEntity>.Filter.Eq("Name", "Fred Flintstone");

            return await Collection.DistinctAsync<string>("Name", filter);
        }

// EstimatedDocumentCount{Async} did not exist in driver version 2.3 which is bound to net462 in MultiFunctionApplicationHelpers.csproj
#if !MONGODRIVER2_3
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public long EstimatedDocumentCount()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            Collection.InsertOne(document);
            return Collection.EstimatedDocumentCount();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<long> EstimatedDocumentCountAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            await Collection.InsertOneAsync(document);
            return await Collection.EstimatedDocumentCountAsync();
        }
#endif

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public IAsyncCursor<BsonDocument> MapReduce()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            Collection.InsertOne(document);

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

#pragma warning disable CS0618 // obsolete
            MapReduceOptions<CustomMongoDbEntity, BsonDocument> options = new MapReduceOptions<CustomMongoDbEntity, BsonDocument>
            {
                Filter = filter,
                OutputOptions = MapReduceOutputOptions.Inline
            };

            return Collection.MapReduce(map, reduce, options);
#pragma warning restore CS0618
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<IAsyncCursor<BsonDocument>> MapReduceAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
            await Collection.InsertOneAsync(document);

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

#pragma warning disable CS0618 // obsolete
            MapReduceOptions<CustomMongoDbEntity, BsonDocument> options = new MapReduceOptions<CustomMongoDbEntity, BsonDocument>
            {
                Filter = filter,
                OutputOptions = MapReduceOutputOptions.Inline
            };

            return await Collection.MapReduceAsync(map, reduce, options);
#pragma warning restore CS0618
        }

#if !MONGODRIVER2_3
        // This call will throw an exception because the Watch() method only works with MongoDb replica sets, but it is fine as long as the method is executed. 
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string Watch()
        {
            try
            {
                var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
                Collection.InsertOne(document);
                Collection.Watch();

                return "Ok";
            }
            catch (MongoCommandException)
            {
                return "Got exception but it is ok!";
            }
        }

        // This call will throw an exception because the Watch() method only works with MongoDb replica sets, but it is fine as long as the method is executed.
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> WatchAsync()
        {
            try
            {
                var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" };
                await Collection.InsertOneAsync(document);
                await Collection.WatchAsync();

                return "Ok";

            }
            catch (MongoCommandException)
            {
                return "Got exception but it is ok!";
            }
        }
#endif

        #endregion

#region Database

#if !MONGODRIVER2_3 && !MONGODRIVER2_8_1

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string AggregateDB()
        {
            var listLocalSessions = new BsonDocument
            {
                {
                    "$listLocalSessions",
                    new BsonDocument
                    {
                        // empty
                    }
                }
            };

            var pipeline = new[] { listLocalSessions };
            var result = Db.Aggregate<BsonDocument>(pipeline);
            return result.FirstOrDefault().ToString();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> AggregateDBAsync()
        {
            var listLocalSessions = new BsonDocument
            {
                {
                    "$listLocalSessions",
                    new BsonDocument
                    {
                        // empty
                    }
                }
            };

            var pipeline = new[] { listLocalSessions };
            var result = await Db.AggregateAsync<BsonDocument>(pipeline);
            return result.FirstOrDefault().ToString();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void AggregateDBToCollection()
        {
            var listLocalSessions = new BsonDocument
            {
                {
                    "$listLocalSessions",
                    new BsonDocument
                    {
                        // empty
                    }
                }
            };

            var outStage = new BsonDocument
            {
                {
                    "$out",
                    new BsonDocument
                    {
                        { "db", _dbName },
                        { "coll", _defaultCollectionName }
                    }
                }
            };

            var pipeline = new[] { listLocalSessions, outStage };
            Db.AggregateToCollection<BsonDocument>(pipeline);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task AggregateDBToCollectionAsync()
        {
            var listLocalSessions = new BsonDocument
            {
                {
                    "$listLocalSessions",
                    new BsonDocument
                    {
                        // empty
                    }
                }
            };

            var outStage = new BsonDocument
            {
                {
                    "$out",
                    new BsonDocument
                    {
                        { "db", _dbName },
                        { "coll", _defaultCollectionName }
                    }
                }
            };

            var pipeline = new[] { listLocalSessions, outStage };
            try
            {
                await Db.AggregateToCollectionAsync<BsonDocument>(pipeline);
            }
            catch
            {
                // This method is throwing an exception in net471 for unknown reasons. However, we just need the method to execute
                // so our instrumentation runs and generates the expected metric, so just swallow the exception.
            }
        }

#endif

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string CreateCollection()
        {
            var collectionName = "createTestCollection";
            Db.CreateCollection(collectionName);
            var collection = Db.GetCollection<BsonDocument>(collectionName);
            Db.DropCollection(collectionName);
            return collection.CollectionNamespace.CollectionName;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> CreateCollectionAsync()
        {
            var collectionName = "createTestCollectionAsync";
            await Db.CreateCollectionAsync(collectionName);
            var collection = Db.GetCollection<BsonDocument>(collectionName);
            await Db.DropCollectionAsync(collectionName);
            return collection.CollectionNamespace.CollectionName;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string DropCollection()
        {
            var collectionName = "dropTestCollection";
            Db.CreateCollection(collectionName);
            var collection = Db.GetCollection<BsonDocument>(collectionName);
            Db.DropCollection(collectionName);
            return collection.CollectionNamespace.CollectionName;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> DropCollectionAsync()
        {
            var collectionName = "dropTestCollectionAsync";
            await Db.CreateCollectionAsync(collectionName);
            var collection = Db.GetCollection<BsonDocument>(collectionName);
            await Db.DropCollectionAsync(collectionName);
            return collection.CollectionNamespace.CollectionName;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int ListCollections()
        {
            var collections = Db.ListCollections().ToList();
            return collections.Count();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<int> ListCollectionsAsync()
        {
            var cursor = await Db.ListCollectionsAsync();
            var collections = cursor.ToList();
            return collections.Count;
        }

#if !MONGODRIVER2_3
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int ListCollectionNames()
        {
            var collections = Db.ListCollectionNames().ToList();
            return collections.Count();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<int> ListCollectionNamesAsync()
        {
            var cursor = await Db.ListCollectionNamesAsync();
            var collections = cursor.ToList();
            return collections.Count;
        }
#endif

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string RenameCollection()
        {
            var collectionName = "NamedCollection";
            var newName = "RenamedCollection";

            var collection = Db.GetCollection<CustomMongoDbEntity>(collectionName);
            EnsureCollectionExists(collection);

            Db.RenameCollection(collectionName, newName);
            var newCollection = Db.GetCollection<BsonDocument>(newName);
            return newCollection.CollectionNamespace.CollectionName;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> RenameCollectionAsync()
        {
            var collectionName = "NamedCollectionAsync";
            var newName = "RenamedCollectionAsync";

            var collection = Db.GetCollection<CustomMongoDbEntity>(collectionName);
            EnsureCollectionExists(collection);

            await Db.RenameCollectionAsync(collectionName, newName);
            var newCollection = Db.GetCollection<BsonDocument>(newName);
            return newCollection.CollectionNamespace.CollectionName;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string RunCommand()
        {
            var command = new BsonDocumentCommand<BsonDocument>(new BsonDocument { { "dbStats", 1 }, { "scale", 1 } });
            var result = Db.RunCommand<BsonDocument>(command);
            return result.ToString();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> RunCommandAsync()
        {
            var command = new BsonDocumentCommand<BsonDocument>(new BsonDocument { { "dbStats", 1 }, { "scale", 1 } });
            var result = await Db.RunCommandAsync<BsonDocument>(command);
            return result.ToString();
        }

#if !MONGODRIVER2_3
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string WatchDB()
        {
            try
            {
                var result = Db.Watch();
                return "Ok";
            }
            catch (MongoCommandException)
            {
                return "Got exception but it is ok!";
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> WatchDBAsync()
        {
            try
            {
                var result = await Db.WatchAsync();
                return "Ok";
            }
            catch (MongoCommandException)
            {
                return "Got exception but it is ok!";
            }
        }

#endif

        #endregion

        #region IndexManager

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int CreateOne()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "" };
            Collection.InsertOne(document);
#if NET462
            Collection.Indexes.CreateOne(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name));
#else
            Collection.Indexes.CreateOne(new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)));
#endif
            return 1;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<int> CreateOneAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "" };
            await Collection.InsertOneAsync(document);
#if NET462
            await Collection.Indexes.CreateOneAsync(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name));
#else
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)));
#endif
            return 1;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int CreateMany()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "" };
            Collection.InsertOne(document);

            var result = Collection.Indexes.CreateMany(new[] { new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)) });
            return result.Count();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<int> CreateManyAsync()
        {
            var document = new CustomMongoDbEntity { Id = new ObjectId(), Name = "" };
            await Collection.InsertOneAsync(document);

            var result = await Collection.Indexes.CreateManyAsync(new[] { new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)) });
            return result.Count();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DropAll()
        {
            Collection.Indexes.DropAll();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task DropAllAsync()
        {
            await Collection.Indexes.DropAllAsync();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DropOne()
        {
            Collection.Indexes.CreateMany(new[] { new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)) });
            Collection.Indexes.DropOne("Name_1");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task DropOneAsync()
        {
            await Collection.Indexes.CreateManyAsync(new[] { new CreateIndexModel<CustomMongoDbEntity>(Builders<CustomMongoDbEntity>.IndexKeys.Ascending(k => k.Name)) });
            await Collection.Indexes.DropOneAsync("Name_1");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int List()
        {
            var cursor = Collection.Indexes.List();
            var result = cursor.ToList();
            return result.Count();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<int> ListAsync()
        {
            var cursor = await Collection.Indexes.ListAsync();
            var result = cursor.ToList();
            return result.Count();
        }

#endregion

#region Linq

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int ExecuteModel()
        {
            var result = Collection.AsQueryable().Where(w => w.Name == "Fred Flintstone");
            return result.Count();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<int> ExecuteModelAsync()
        {
            var cursor = await Collection.AsQueryable().Where(w => w.Name == "Fred Flintstone").ToCursorAsync();
            var result = cursor.ToList().Count();
            return result;
        }

#endregion

#region AsyncCursor

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int GetNextBatch()
        {

            Collection.InsertMany(new[]
            {
                new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" },
                new CustomMongoDbEntity { Id = new ObjectId(), Name = "Alan Flintstone" }
            });

            var filter = Builders<CustomMongoDbEntity>.Filter.Exists("Name");

            var cursor = Collection.FindSync(filter, new FindOptions<CustomMongoDbEntity, CustomMongoDbEntity>()
            {
                BatchSize = 1
            });

            var result = cursor.ToList();
            return result.Count;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<int> GetNextBatchAsync()
        {

            await Collection.InsertManyAsync(new[]
            {
                new CustomMongoDbEntity { Id = new ObjectId(), Name = "Fred Flintstone" },
                new CustomMongoDbEntity { Id = new ObjectId(), Name = "Alan Flintstone" }
            });

            var filter = Builders<CustomMongoDbEntity>.Filter.Exists("Name");

            var cursor = await Collection.FindAsync(filter, new FindOptions<CustomMongoDbEntity, CustomMongoDbEntity>()
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

            var collection = Db.GetCollection<CustomMongoDbEntity>(collectionName);

            if (collection == null)
            {
                Db.CreateCollection(collectionName);
                collection = Db.GetCollection<CustomMongoDbEntity>(collectionName);
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
            Db.DropCollection(collectionName);
        }

#endregion

    }

}
