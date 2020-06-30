/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Linq;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared;

namespace MongoDB2_6CoreApplication
{
    public class MongoDB2_6Api
    {
        const string CollectionName = "myCoreCollection";

        private readonly IMongoDatabase _db;
        private readonly string _defaultCollectionName;
        private readonly IMongoClient _client;

        public MongoDB2_6Api(string databaseName = "myCoreDb")
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
            var document = new CustomMongoDB2_6Entity { Id = new ObjectId(), Name = "Fred Flintstone" };
            collection.InsertOne(document);
        }

        public async Task InsertOneAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDB2_6Entity { Id = new ObjectId(), Name = "Fred 'Async' Flintstone" };
            await collection.InsertOneAsync(document);
        }

        #endregion

        #region Update

        public UpdateResult UpdateOne()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDB2_6Entity { Id = new ObjectId(), Name = "Dino Flintstone" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDB2_6Entity>.Filter.Eq("name", "Dino Flintstone");
            var update = Builders<CustomMongoDB2_6Entity>.Update.Set("name", "Dinosaur Flintstone");
            var result = collection.UpdateOne(filter, update);
            return result;
        }

        public async Task<UpdateResult> UpdateOneAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDB2_6Entity { Id = new ObjectId(), Name = "Dino Flintstone" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDB2_6Entity>.Filter.Eq("name", "Dino 'Async' Flintstone");
            var update = Builders<CustomMongoDB2_6Entity>.Update.Set("name", "Dinosaur 'Async' Flintstone");
            var result = await collection.UpdateOneAsync(filter, update);
            return result;
        }

        #endregion

        #region Delete

        public DeleteResult DeleteOne()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDB2_6Entity { Id = new ObjectId(), Name = "Barney Rubble" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDB2_6Entity>.Filter.Eq("name", "Barney Rubble");
            var result = collection.DeleteOne(filter);
            return result;
        }

        public async Task<DeleteResult> DeleteOneAsync()
        {
            var collection = GetAddCollection();
            var document = new CustomMongoDB2_6Entity { Id = new ObjectId(), Name = "Barney 'Async' Rubble" };
            collection.InsertOne(document);

            var filter = Builders<CustomMongoDB2_6Entity>.Filter.Eq("name", "Barney 'Async' Rubble");
            var result = await collection.DeleteOneAsync(filter);
            return result;
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

        #region Helpers

        private IMongoCollection<CustomMongoDB2_6Entity> GetAddCollection(string collectionName = null)
        {
            collectionName = collectionName ?? _defaultCollectionName;

            var collection = _db.GetCollection<CustomMongoDB2_6Entity>(collectionName);

            if (collection == null)
            {
                _db.CreateCollection(collectionName);
                collection = _db.GetCollection<CustomMongoDB2_6Entity>(collectionName);
            }

            return collection;
        }

        #endregion
    }
}
