// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MongoDB2_6CoreApplication.Controllers
{
    public class MongoDBController : Controller
    {
        [HttpGet]
        [Route("api/MongoDB/DropDatabase")]
        public void DropDatabase(string dbName = "myCoreDb")
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            api.DropDatabase(dbName);
        }

        [HttpGet]
        [Route("api/MongoDB/InsertOne")]
        public string InsertOne(string dbName = "myCoreDb")
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            api.InsertOne();
            return "InsertOne Called";
        }

        [HttpGet]
        [Route("api/MongoDB/InsertOneAsync")]
        public async Task<string> InsertOneAsync(string dbName = "myCoreDb")
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            await api.InsertOneAsync();
            return "InsertOneAsync Called";
        }

        [HttpGet]
        [Route("api/MongoDB/UpdateOne")]
        public string UpdateOne(string dbName = "myCoreDb")
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.UpdateOne();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/UpdateOneAsync")]
        public async Task<string> UpdateOneAsync(string dbName = "myCoreDb")
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.UpdateOneAsync();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/DeleteOne")]
        public string DeleteOne(string dbName = "myCoreDb")
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.DeleteOne();
            return "Deleted Count: " + result.DeletedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/DeleteOneAsync")]
        public async Task<string> DeleteOneAsync(string dbName = "myCoreDb")
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.DeleteOneAsync();
            return "Deleted Count: " + result.DeletedCount;
        }

        #region Linq

        [HttpGet]
        [Route("api/MongoDB/ExecuteModel")]
        public string ExecuteModel(string dbName = "myCoreDb")
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = api.ExecuteModel();
            return "Record Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/ExecuteModelAsync")]
        public async Task<string> ExecuteModelAsync(string dbName = "myCoreDb")
        {
            var api = new MongoDbApi.MongoDbApi(dbName);
            var result = await api.ExecuteModelAsync();
            return "Record Count: " + result;
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
    }
}
