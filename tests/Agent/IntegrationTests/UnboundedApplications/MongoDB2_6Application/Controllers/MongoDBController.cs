// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using System.Web.Http;
using MongoDB.Driver;

using System.Threading.Tasks;
using MongoDB.Bson;

namespace MongoDB2_6Application.Controllers
{
    public class MongoDBController : ApiController
    {

        #region Drop

        [HttpGet]
        [ActionName("DropDatabase")]
        public void DropDatabase(string dbName)
        {
            var api = new MongoDB2_6Api();
            api.DropDatabase(dbName);
        }

        #endregion

        #region Insert

        [HttpGet]
        [ActionName("InsertOne")]
        public string InsertOne(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            api.InsertOne();
            return "InsertOne Called";
        }

        [HttpGet]
        [ActionName("InsertOneAsync")]
        public async Task<string> InsertOneAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            await api.InsertOneAsync();
            return "InsertOneAsync Called";
        }

        [HttpGet]
        [ActionName("InsertMany")]
        public string InsertMany(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            api.InsertMany();
            return "InsertMany Called";
        }

        [HttpGet]
        [ActionName("InsertManyAsync")]
        public async Task<string> InsertManyAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            await api.InsertManyAsync();
            return "InsertManyAsync Called";
        }

        #endregion

        #region Replace
        [HttpGet]
        [ActionName("ReplaceOne")]
        public string ReplaceOne(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            api.ReplaceOne();
            return "ReplaceOne Called";
        }

        [HttpGet]
        [ActionName("ReplaceOneAsync")]
        public async Task<string> ReplaceOneAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            await api.ReplaceOneAsync();
            return "ReplaceOneAsync Called";
        }
        #endregion

        #region Update

        [HttpGet]
        [ActionName("UpdateOne")]
        public string UpdateOne(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.UpdateOne();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [ActionName("UpdateOneAsync")]
        public async Task<string> UpdateOneAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.UpdateOneAsync();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [ActionName("UpdateMany")]
        public string UpdateMany(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.UpdateMany();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [ActionName("UpdateManyAsync")]
        public async Task<string> UpdateManyAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.UpdateManyAsync();
            return "Modified Count: " + result.ModifiedCount;
        }

        #endregion

        #region Delete

        [HttpGet]
        [ActionName("DeleteOne")]
        public string DeleteOne(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.DeleteOne();
            return "Deleted Count: " + result.DeletedCount;
        }

        [HttpGet]
        [ActionName("DeleteOneAsync")]
        public async Task<string> DeleteOneAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.DeleteOneAsync();
            return "Deleted Count: " + result.DeletedCount;
        }

        [HttpGet]
        [ActionName("DeleteMany")]
        public string DeleteMany(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.DeleteMany();
            return "Deleted Count: " + result.DeletedCount;
        }

        [HttpGet]
        [ActionName("DeleteManyAsync")]
        public async Task<string> DeleteManyAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.DeleteManyAsync();
            return "Deleted Count: " + result.DeletedCount;
        }

        #endregion

        #region Find

        [HttpGet]
        [ActionName("FindSync")]
        public string FindSync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var cursor = api.FindSync();
            var name = cursor.Any() ? cursor.First<CustomMongoDB2_6Entity>().Name : "none";
            return "Entity Name: " + name;
        }

        [HttpGet]
        [ActionName("FindAsync")]
        public async Task<string> FindAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var cursor = await api.FindAsync();
            var name = cursor.Any() ? cursor.First<CustomMongoDB2_6Entity>().Name : "none";
            return "Entity Name: " + name;
        }

        [HttpGet]
        [ActionName("FindOneAndDelete")]
        public string FindOneAndDelete(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var entity = api.FindOneAndDelete();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [ActionName("FindOneAndDeleteAsync")]
        public async Task<string> FindOneAndDeleteAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var entity = await api.FindOneAndDeleteAsync();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [ActionName("FindOneAndReplace")]
        public string FindOneAndReplace(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var entity = api.FindOneAndReplace();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [ActionName("FindOneAndReplaceAsync")]
        public async Task<string> FindOneAndReplaceAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var entity = await api.FindOneAndReplaceAsync();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [ActionName("FindOneAndUpdate")]
        public string FindOneAndUpdate(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var entity = api.FindOneAndUpdate();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        [HttpGet]
        [ActionName("FindOneAndUpdateAsync")]
        public async Task<string> FindOneAndUpdateAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var entity = await api.FindOneAndUpdateAsync();
            return "Entity Name: " + (entity == null ? "null entity" : entity.Name);
        }

        #endregion

        #region Other API

        [HttpGet]
        [ActionName("BulkWrite")]
        public string BulkWrite(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.BulkWrite();
            return "Request Count: " + result.RequestCount;
        }


        [HttpGet]
        [ActionName("BulkWriteAsync")]
        public async Task<string> BulkWriteAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.BulkWriteAsync();
            return "Request Count: " + result.RequestCount;
        }

        [HttpGet]
        [ActionName("Aggregate")]
        public string Aggregate(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.Aggregate();

            var name = result.Any() ? result.First<CustomMongoDB2_6Entity>().Name : "none";

            return "Aggregate Name: " + name;

        }

        [HttpGet]
        [ActionName("Count")]
        public string Count(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.Count();
            return "Count: " + result;
        }

        [HttpGet]
        [ActionName("CountAsync")]
        public async Task<string> CountAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.CountAsync();
            return "Count: " + result;
        }

        [HttpGet]
        [ActionName("Distinct")]
        public string Distinct(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.Distinct();

            var name = result.Any() ? result.First() : "none";
            return "Name: " + name;
        }

        [HttpGet]
        [ActionName("DistinctAsync")]
        public async Task<string> DistinctAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.DistinctAsync();

            var name = result.Any() ? result.First<string>() : "none";
            return "Name: " + name;
        }

        [HttpGet]
        [ActionName("MapReduce")]
        public string MapReduce(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.MapReduce();

            var json = result.Any() ? result.First().ToJson() : "none";
            return "Result: " + json;
        }

        [HttpGet]
        [ActionName("MapReduceAsync")]
        public async Task<string> MapReduceAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.MapReduceAsync();

            var json = result.Any() ? result.First().ToJson() : "none";
            return "Result: " + json;
        }


        [HttpGet]
        [ActionName("Watch")]
        public string Watch(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.Watch();

            return result;
        }

        [HttpGet]
        [ActionName("WatchAsync")]
        public async Task<string> WatchAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.WatchAsync();

            return result;
        }

        #endregion

        #region Linq

        [HttpGet]
        [ActionName("ExecuteModel")]
        public string ExecuteModel(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.ExecuteModel();
            return "Record Count: " + result;
        }

        [HttpGet]
        [ActionName("ExecuteModelAsync")]
        public async Task<string> ExecuteModelAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.ExecuteModelAsync();
            return "Record Count: " + result;
        }

        #endregion

        #region Database

        [HttpGet]
        [ActionName("CreateCollection")]
        public string CreateCollection(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.CreateCollection();
            return "CollectionName: " + result;
        }

        [HttpGet]
        [ActionName("CreateCollectionAsync")]
        public async Task<string> CreateCollectionAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.CreateCollectionAsync();
            return "CollectionName: " + result;
        }

        [HttpGet]
        [ActionName("DropCollection")]
        public string DropCollection(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.DropCollection();
            return "CollectionName: " + result;
        }

        [HttpGet]
        [ActionName("DropCollectionAsync")]
        public async Task<string> DropCollectionAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.DropCollectionAsync();
            return "CollectionName: " + result;
        }

        [HttpGet]
        [ActionName("ListCollections")]
        public string ListCollections(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.ListCollections();
            return "Collection Count: " + result;
        }

        [HttpGet]
        [ActionName("ListCollectionsAsync")]
        public async Task<string> ListCollectionsAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.ListCollectionsAsync();
            return "Collection Count: " + result;
        }

        [HttpGet]
        [ActionName("RenameCollection")]
        public string RenameCollection(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.RenameCollection();
            return "Collection Name: " + result;
        }

        [HttpGet]
        [ActionName("RenameCollectionAsync")]
        public async Task<string> RenameCollectionAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.RenameCollectionAsync();
            return "Collection Name: " + result;
        }

        [HttpGet]
        [ActionName("RunCommand")]
        public string RunCommand(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.RunCommand();
            return "Command: " + result;
        }

        [HttpGet]
        [ActionName("RunCommandAsync")]
        public async Task<string> RunCommandAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.RunCommandAsync();
            return "Command: " + result;
        }

        #endregion

        #region IndexManager

        [HttpGet]
        [ActionName("CreateOne")]
        public string CreateOne(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.CreateOne();
            return "Index Count: " + result;
        }

        [HttpGet]
        [ActionName("CreateOneAsync")]
        public async Task<string> CreateOneAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.CreateOneAsync();
            return "Index Count: " + result;
        }

        [HttpGet]
        [ActionName("CreateMany")]
        public string CreateMany(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.CreateMany();
            return "Index Count: " + result;
        }

        [HttpGet]
        [ActionName("CreateManyAsync")]
        public async Task<string> CreateManyAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.CreateManyAsync();
            return "Index Count: " + result;
        }

        [HttpGet]
        [ActionName("DropAll")]
        public void DropAll(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            api.DropAll();
        }

        [HttpGet]
        [ActionName("DropAllAsync")]
        public async Task DropAllAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            await api.DropAllAsync();
        }

        [HttpGet]
        [ActionName("DropOne")]
        public void DropOne(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            api.DropOne();
        }

        [HttpGet]
        [ActionName("DropOneAsync")]
        public async Task DropOneAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            await api.DropOneAsync();
        }

        [HttpGet]
        [ActionName("List")]
        public string List(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.List();
            return "Index Count: " + result;
        }

        [HttpGet]
        [ActionName("ListAsync")]
        public async Task<string> ListAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.ListAsync();
            return "Index Count: " + result;
        }

        #endregion

        #region AsyncCursor

        [HttpGet]
        [ActionName("GetNextBatch")]
        public string MoveNext(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.GetNextBatch();
            return "Record Count: " + result;
        }

        [HttpGet]
        [ActionName("GetNextBatchAsync")]
        public async Task<string> MoveNextAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.GetNextBatchAsync();
            return "Any: " + result;
        }

        #endregion

    }
}
