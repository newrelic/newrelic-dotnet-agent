/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Linq;
using System.Web.Http;
using System.Threading;
using MongoDBApplication;

namespace MongoDBApplication.Controllers
{
    public class MongoDBController : ApiController
    {
        #region Find API
        [HttpGet]
        [ActionName("Find")]
        public string Find()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            api.Insert();
            var cursor = api.Find();
            var entity = cursor.First();
            return "Entity name: " + entity.Name;
        }

        [HttpGet]
        [ActionName("FindOne")]
        public string FindOne()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            api.Insert();
            var entity = api.FindOne();
            return "Entity name: " + entity.Name;
        }

        [HttpGet]
        [ActionName("FindOneById")]
        public string FindOneById()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            api.Insert();
            var entity = api.FindOneById();
            return "Entity name: " + entity.Name;
        }

        [HttpGet]
        [ActionName("FindOneAs")]
        public string FindOneAs()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            api.Insert();
            var bsonDocument = api.FindOneAs();
            return "BsonDocument: " + bsonDocument.ToString();
        }

        [HttpGet]
        [ActionName("FindAll")]
        public string FindAll()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            api.Insert();
            var all = api.FindAll();

            return "FindAll on MongoDB Collection.";
        }
        #endregion

        #region Insert Update Remove API

        [HttpGet]
        [ActionName("Insert")]
        public string Insert()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            api.Insert();
            return "Insert on MongoDB Collection.";
        }

        [HttpGet]
        [ActionName("OrderedBulkInsert")]
        public string OrderedBulkInsert()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var result = api.OrderedBulkInsert();
            return result;
        }

        [HttpGet]
        [ActionName("UnorderedBulkInsert")]
        public string UnorderedBulkInsert()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var result = api.UnorderedBulkInsert();
            return result;
        }

        [HttpGet]
        [ActionName("Update")]
        public string Update()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var result = api.Update();
            return result;
        }

        [HttpGet]
        [ActionName("Remove")]
        public string Remove()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var result = api.Remove();
            return result;
        }

        [HttpGet]
        [ActionName("RemoveAll")]
        public string RemoveAll()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var result = api.RemoveAll();
            return result;
        }

        [HttpGet]
        [ActionName("Drop")]
        public string Drop()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var result = api.Drop();
            return result;
        }
        #endregion

        #region FindAnd API
        [HttpGet]
        [ActionName("FindAndModify")]
        public string FindAndModify()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var result = api.FindAndModify();
            return result;
        }

        [HttpGet]
        [ActionName("FindAndRemove")]
        public string FindAndRemove()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var result = api.FindAndRemove();
            return result;
        }
        #endregion

        #region Generic Find API
        [HttpGet]
        [ActionName("GenericFind")]
        public string GenericFind()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var cursor = api.GenericFind();
            var entity = cursor.First();
            return "Entity name: " + entity.Name;
        }

        [HttpGet]
        [ActionName("GenericFindAs")]
        public string GenericFindAs()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var cursor = api.GenericFindAs();
            var entity = cursor.First();
            return "Entity name: " + entity.Name;
        }

        [HttpGet]
        [ActionName("CursorGetEnumerator")]
        public string CursorGetEnumerator()
        {
            Thread.Sleep(1000);

            var api = new MongoDbApi();
            var enumerator = api.CursorGetEnumerator();

            while (enumerator.MoveNext())
            {
                var entity = enumerator.Current;
                if (entity.Name.Length > 0)
                {
                    return entity.Name;
                }
            }
            return enumerator.ToString();
        }

        [HttpGet]
        [ActionName("MongoCursorEnumeratorMoveNext")]
        public string MongoCursorEnumeratorMoveNext()
        {
            Thread.Sleep(3000);

            var api = new MongoDbApi();
            var result = api.CursorMoveNext();
            return result.ToString();
        }
        #endregion

        #region Index API
        [HttpGet]
        [ActionName("CreateIndex")]
        public string CreateIndex()
        {
            Thread.Sleep(3000);

            var api = new MongoDbApi();
            var result = api.CreateIndex();
            return result.ToString();
        }

        [HttpGet]
        [ActionName("GetIndexes")]
        public string GetIndexes()
        {
            Thread.Sleep(3000);

            var api = new MongoDbApi();
            var result = api.GetIndexes();
            return result.ToString();
        }

        [HttpGet]
        [ActionName("IndexExistsByName")]
        public string IndexExistsByName()
        {
            Thread.Sleep(3000);

            var api = new MongoDbApi();
            var result = api.IndexExistsByName();
            return result.ToString();
        }
        #endregion

        #region Aggregate API
        [HttpGet]
        [ActionName("Aggregate")]
        public string Aggregate()
        {
            Thread.Sleep(3000);

            var api = new MongoDbApi();
            var result = api.Aggregate();
            return result.ToString();
        }

        [HttpGet]
        [ActionName("AggregateExplain")]
        public string AggregateExplain()
        {
            Thread.Sleep(3000);

            var api = new MongoDbApi();
            var result = api.AggregateExplain();
            return result.ToString();
        }
        #endregion

        #region Other API
        [HttpGet]
        [ActionName("Validate")]
        public string Validate()
        {
            Thread.Sleep(3000);

            var api = new MongoDbApi();
            var result = api.Validate();
            return result.ToString();
        }

        [HttpGet]
        [ActionName("ParallelScanAs")]
        public string ParallelScanAs()
        {
            Thread.Sleep(3000);

            var api = new MongoDbApi();
            var result = api.ParallelScanAs();
            return result.ToString();
        }

        [HttpGet]
        [ActionName("CreateCollection")]
        public string CreateCollection()
        {
            Thread.Sleep(3000);

            var api = new MongoDbApi();
            var result = api.CreateCollection();
            return result.ToString();
        }

        #endregion
    }
}

