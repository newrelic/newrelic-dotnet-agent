// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MongoDB2_6CoreApplication.Controllers
{
    public class MongoDBController : Controller
    {
        #region Drop

        [HttpGet]
        [Route("api/MongoDB/DropDatabase")]
        public void DropDatabase(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            api.DropDatabase(dbName);
        }

        #endregion

        #region Insert

        [HttpGet]
        [Route("api/MongoDB/InsertOne")]
        public string InsertOne(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            api.InsertOne();
            return "InsertOne Called";
        }

        [HttpGet]
        [Route("api/MongoDB/InsertOneAsync")]
        public async Task<string> InsertOneAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            await api.InsertOneAsync();
            return "InsertOneAsync Called";
        }

        [HttpGet]
        [Route("api/MongoDB/InsertMany")]
        public string InsertMany(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            api.InsertMany();
            return "InsertMany Called";
        }

        [HttpGet]
        [Route("api/MongoDB/InsertManyAsync")]
        public async Task<string> InsertManyAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            await api.InsertManyAsync();
            return "InsertManyAsync Called";
        }

        #endregion

        #region Replace
        [HttpGet]
        [Route("api/MongoDB/ReplaceOne")]
        public string ReplaceOne(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            api.ReplaceOne();
            return "ReplaceOne Called";
        }

        [HttpGet]
        [Route("api/MongoDB/ReplaceOneAsync")]
        public async Task<string> ReplaceOneAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            await api.ReplaceOneAsync();
            return "ReplaceOneAsync Called";
        }
        #endregion

        #region Update

        [HttpGet]
        [Route("api/MongoDB/UpdateOne")]
        public string UpdateOne(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.UpdateOne();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/UpdateOneAsync")]
        public async Task<string> UpdateOneAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.UpdateOneAsync();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/UpdateMany")]
        public string UpdateMany(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.UpdateMany();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/UpdateManyAsync")]
        public async Task<string> UpdateManyAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.UpdateManyAsync();
            return "Modified Count: " + result.ModifiedCount;
        }

        #endregion

        #region Delete

        [HttpGet]
        [Route("api/MongoDB/DeleteOne")]
        public string DeleteOne(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.DeleteOne();
            return "Deleted Count: " + result.DeletedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/DeleteOneAsync")]
        public async Task<string> DeleteOneAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.DeleteOneAsync();
            return "Deleted Count: " + result.DeletedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/DeleteMany")]
        public string DeleteMany(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.DeleteMany();
            return "Deleted Count: " + result.DeletedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/DeleteManyAsync")]
        public async Task<string> DeleteManyAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.DeleteManyAsync();
            return "Deleted Count: " + result.DeletedCount;
        }

        #endregion

        #region Find

        [HttpGet]
        [Route("api/MongoDB/FindSync")]
        public string FindSync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var cursor = api.FindSync();
            var name = cursor.FirstOrDefault();
            return "Entity Name: " + name;
        }

        [HttpGet]
        [Route("api/MongoDB/FindAsync")]
        public async Task<string> FindAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var cursor = await api.FindAsync();
            var name = cursor.FirstOrDefault();
            return "Entity Name: " + name;
        }

        [HttpGet]
        [Route("api/MongoDB/FindOneAndDelete")]
        public string FindOneAndDelete(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var entity = api.FindOneAndDelete();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [Route("api/MongoDB/FindOneAndDeleteAsync")]
        public async Task<string> FindOneAndDeleteAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var entity = await api.FindOneAndDeleteAsync();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [Route("api/MongoDB/FindOneAndReplace")]
        public string FindOneAndReplace(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var entity = api.FindOneAndReplace();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [Route("api/MongoDB/FindOneAndReplaceAsync")]
        public async Task<string> FindOneAndReplaceAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var entity = await api.FindOneAndReplaceAsync();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [Route("api/MongoDB/FindOneAndUpdate")]
        public string FindOneAndUpdate(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var entity = api.FindOneAndUpdate();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [Route("api/MongoDB/FindOneAndUpdateAsync")]
        public async Task<string> FindOneAndUpdateAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var entity = await api.FindOneAndUpdateAsync();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        #endregion

        #region Other API

        [HttpGet]
        [Route("api/MongoDB/BulkWrite")]
        public string BulkWrite(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.BulkWrite();
            return "Request Count: " + result.RequestCount;
        }


        [HttpGet]
        [Route("api/MongoDB/BulkWriteAsync")]
        public async Task<string> BulkWriteAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.BulkWriteAsync();
            return "Request Count: " + result.RequestCount;
        }

        [HttpGet]
        [Route("api/MongoDB/Aggregate")]
        public string Aggregate(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.Aggregate();

            var name = result.First<MongoDbApi.CustomMongoDbEntity>().Name;

            return "Aggregate Name: " + name;

        }

        [HttpGet]
        [Route("api/MongoDB/Count")]
        public string Count(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.Count();
            return "Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/CountAsync")]
        public async Task<string> CountAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.CountAsync();
            return "Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/Distinct")]
        public string Distinct(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.Distinct();

            var name = result.First();
            return "Name: " + name;
        }

        [HttpGet]
        [Route("api/MongoDB/DistinctAsync")]
        public async Task<string> DistinctAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.DistinctAsync();

            var name = result.First();
            return "Name: " + name;
        }

        [HttpGet]
        [Route("api/MongoDB/MapReduce")]
        public string MapReduce(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.MapReduce();

            var json = result.First().ToJson();
            return "Result: " + json;
        }

        [HttpGet]
        [Route("api/MongoDB/MapReduceAsync")]
        public async Task<string> MapReduceAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.MapReduceAsync();

            var json = result.First().ToJson();
            return "Result: " + json;
        }


        [HttpGet]
        [Route("api/MongoDB/Watch")]
        public string Watch(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.Watch();

            return result;
        }

        [HttpGet]
        [Route("api/MongoDB/WatchAsync")]
        public async Task<string> WatchAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.WatchAsync();

            return result;
        }

        #endregion

        #region Linq

        [HttpGet]
        [Route("api/MongoDB/ExecuteModel")]
        public string ExecuteModel(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.ExecuteModel();
            return "Record Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/ExecuteModelAsync")]
        public async Task<string> ExecuteModelAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.ExecuteModelAsync();
            return "Record Count: " + result;
        }

        #endregion

        #region Database

        [HttpGet]
        [Route("api/MongoDB/CreateCollection")]
        public string CreateCollection(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.CreateCollection();
            return "CollectionName: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/CreateCollectionAsync")]
        public async Task<string> CreateCollectionAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.CreateCollectionAsync();
            return "CollectionName: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/DropCollection")]
        public string DropCollection(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.DropCollection();
            return "CollectionName: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/DropCollectionAsync")]
        public async Task<string> DropCollectionAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.DropCollectionAsync();
            return "CollectionName: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/ListCollections")]
        public string ListCollections(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.ListCollections();
            return "Collection Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/ListCollectionsAsync")]
        public async Task<string> ListCollectionsAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.ListCollectionsAsync();
            return "Collection Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/RenameCollection")]
        public string RenameCollection(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.RenameCollection();
            return "Collection Name: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/RenameCollectionAsync")]
        public async Task<string> RenameCollectionAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.RenameCollectionAsync();
            return "Collection Name: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/RunCommand")]
        public string RunCommand(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.RunCommand();
            return "Command: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/RunCommandAsync")]
        public async Task<string> RunCommandAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.RunCommandAsync();
            return "Command: " + result;
        }

        #endregion

        #region IndexManager

        [HttpGet]
        [Route("api/MongoDB/CreateOne")]
        public string CreateOne(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.CreateOne();
            return "Index Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/CreateOneAsync")]
        public async Task<string> CreateOneAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.CreateOneAsync();
            return "Index Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/CreateMany")]
        public string CreateMany(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.CreateMany();
            return "Index Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/CreateManyAsync")]
        public async Task<string> CreateManyAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.CreateManyAsync();
            return "Index Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/DropAll")]
        public void DropAll(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            api.DropAll();
        }

        [HttpGet]
        [Route("api/MongoDB/DropAllAsync")]
        public async Task DropAllAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            await api.DropAllAsync();
        }

        [HttpGet]
        [Route("api/MongoDB/DropOne")]
        public void DropOne(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            api.DropOne();
        }

        [HttpGet]
        [Route("api/MongoDB/DropOneAsync")]
        public async Task DropOneAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            await api.DropOneAsync();
        }

        [HttpGet]
        [Route("api/MongoDB/List")]
        public string List(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.List();
            return "Index Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/ListAsync")]
        public async Task<string> ListAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.ListAsync();
            return "Index Count: " + result;
        }

        #endregion

        #region AsyncCursor

        [HttpGet]
        [Route("api/MongoDB/GetNextBatch")]
        public string MoveNext(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.GetNextBatch();
            return "Record Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/GetNextBatchAsync")]
        public async Task<string> MoveNextAsync(string dbName)
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.GetNextBatchAsync();
            return "Any: " + result;
        }

        #endregion

    }
}
