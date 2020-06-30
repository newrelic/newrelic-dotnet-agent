/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MongoDB2_6CoreApplication.Controllers
{
    public class MongoDBController : Controller
    {
        [HttpGet]
        [Route("api/MongoDB/DropDatabase")]
        public void DropDatabase(string dbName)
        {
            var api = new MongoDB2_6Api();
            api.DropDatabase(dbName);
        }

        [HttpGet]
        [Route("api/MongoDB/InsertOne")]
        public string InsertOne(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            api.InsertOne();
            return "InsertOne Called";
        }

        [HttpGet]
        [Route("api/MongoDB/InsertOneAsync")]
        public async Task<string> InsertOneAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            await api.InsertOneAsync();
            return "InsertOneAsync Called";
        }

        [HttpGet]
        [Route("api/MongoDB/UpdateOne")]
        public string UpdateOne(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.UpdateOne();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/UpdateOneAsync")]
        public async Task<string> UpdateOneAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.UpdateOneAsync();
            return "Modified Count: " + result.ModifiedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/DeleteOne")]
        public string DeleteOne(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.DeleteOne();
            return "Deleted Count: " + result.DeletedCount;
        }

        [HttpGet]
        [Route("api/MongoDB/DeleteOneAsync")]
        public async Task<string> DeleteOneAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.DeleteOneAsync();
            return "Deleted Count: " + result.DeletedCount;
        }

        #region Linq

        [HttpGet]
        [Route("api/MongoDB/ExecuteModel")]
        public string ExecuteModel(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = api.ExecuteModel();
            return "Record Count: " + result;
        }

        [HttpGet]
        [Route("api/MongoDB/ExecuteModelAsync")]
        public async Task<string> ExecuteModelAsync(string dbName)
        {
            var api = new MongoDB2_6Api(dbName);
            var result = await api.ExecuteModelAsync();
            return "Record Count: " + result;
        }

        #endregion
    }
}
