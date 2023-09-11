// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Couchbase;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.Couchbase;
using ServiceStack;
using Couchbase.Core;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.Views;
using Couchbase.Configuration.Client;

namespace BasicMvcApplication.Controllers
{
    public class CouchbaseController : Controller
    {
        private readonly string[] _documentIds = new string[] { "airline_10765", "route_5639" };
        private const string _documentId = "airline_10765";
        private readonly CouchbaseConnection _connection;

        public CouchbaseController()
        {
            _connection = new CouchbaseConnection();
            _connection.Connect();
        }

        protected override void Dispose(bool disposing)
        {
            _connection.Dispose();
            base.Dispose(disposing);
        }

        #region Get Methods

        [HttpGet]
        public string Couchbase_Get()
        {
            var bucket = _connection.Bucket;
            var doc = bucket.Get<dynamic>(_documentId).Value.ToString();
            return doc;
        }

        [HttpGet]
        public string Couchbase_GetMultiple()
        {
            var bucket = _connection.Bucket;
            var docCount = bucket.Get<dynamic>(_documentIds).Values.Count;
            return docCount.ToString();
        }

        [HttpGet]
        public string Couchbase_GetMultipleParallelOptions()
        {
            var bucket = _connection.Bucket;
            var parallelOptions = new ParallelOptions()
            {
                CancellationToken = CancellationToken.None,
                MaxDegreeOfParallelism = 1,
                TaskScheduler = TaskScheduler.Current
            };

            var docCount = bucket.Get<dynamic>(_documentIds, parallelOptions).Values.Count;
            return docCount.ToString();
        }

        [HttpGet]
        public string Couchbase_GetMultipleParallelOptionsWithRangeSize()
        {
            var bucket = _connection.Bucket;

            var parallelOptions = new ParallelOptions()
            {
                CancellationToken = CancellationToken.None,
                MaxDegreeOfParallelism = 1,
                TaskScheduler = TaskScheduler.Current
            };

            var docCount = bucket.Get<dynamic>(_documentIds, parallelOptions, 10).Values.Count;
            return docCount.ToString();
        }

        [HttpGet]
        public string Couchbase_GetAndTouch(string documentId)
        {
            var bucket = _connection.Bucket;

            var newDoc = new Document<string>()
            {
                Id = documentId,
                Content = "Hello World!"
            };

            var doc = bucket.GetAndTouch<dynamic>(documentId, TimeSpan.FromSeconds(1)).Value.ToString();
            return doc;
        }

        [HttpGet]
        public string Couchbase_GetWithLock()
        {
            var bucket = _connection.Bucket;
            var doc = bucket.GetAndLock<dynamic>(_documentId, 5).ToJson();
            return doc;
        }

        [HttpGet]
        public string Couchbase_GetDocument()
        {
            var bucket = _connection.Bucket;
            var doc = bucket.GetDocument<dynamic>(_documentId).ToJson();
            return doc;
        }

        [HttpGet]
        public string Couchbase_GetFromReplica()
        {
            var bucket = _connection.Bucket;
            var doc = bucket.GetFromReplica<dynamic>(_documentId).ToJson();
            return doc;
        }

        [HttpGet]
        public async Task<string> Couchbase_GetAsync()
        {
            var bucket = _connection.Bucket;
            var doc = await bucket.GetAsync<dynamic>(_documentId);
            return doc.ToJson();
        }

        [HttpGet]
        public async Task<string> Couchbase_GetAndTouchAsync(string documentId)
        {
            var bucket = _connection.Bucket;

            var newDoc = new Document<string>()
            {
                Id = documentId,
                Content = "Hello World!"
            };

            var doc = await bucket.GetAndTouchAsync<dynamic>(documentId, TimeSpan.FromSeconds(1));
            return doc.ToJson();
        }

        [HttpGet]
        public async Task<string> Couchbase_GetDocumentAsync()
        {
            var bucket = _connection.Bucket;
            var doc = await bucket.GetDocumentAsync<dynamic>(_documentId);
            return doc.ToJson();

        }

        [HttpGet]
        public async Task<string> Couchbase_GetFromReplicaAsync()
        {
            var bucket = _connection.Bucket;
            var doc = await bucket.GetFromReplicaAsync<dynamic>(_documentId);
            return doc.ToJson();
        }

        [HttpGet]
        public async Task<string> Couchbase_GetWithLockAsync()
        {
            var bucket = _connection.Bucket;
            var doc = await bucket.GetAndLockAsync<dynamic>(_documentId, TimeSpan.FromSeconds(5));
            return doc.ToJson();
        }

        #endregion

        #region Insert Methods
        [HttpGet]
        public string Couchbase_Insert(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Insert(document.Id, document.Content);
            return result.Success.ToString();
        }

        [HttpGet]
        public string Couchbase_InsertDocument(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Insert(document);
            return result.Success.ToString();
        }

        [HttpGet]
        public string Couchbase_InsertWithExpiration(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Insert(document.Id, document.Content, 10);
            return result.Success.ToString();
        }

        [HttpGet]
        public string Couchbase_InsertReplicatePersist(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Insert(document.Id, document.Content, ReplicateTo.One, PersistTo.One);
            return result.Success.ToString();
        }

        [HttpGet]
        public string Couchbase_InsertReplicatePersistWithExpiration(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Insert(document.Id, document.Content, 10, ReplicateTo.One, PersistTo.One);
            return result.Success.ToString();
        }

        [HttpGet]
        public async Task<string> Couchbase_InsertAsync(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = await bucket.InsertAsync(document);

            return result.Success.ToString();
        }

        public async Task<string> Couchbase_InsertReplicatePersistAsync(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = await bucket.InsertAsync(document, ReplicateTo.One, PersistTo.One);

            return result.Success.ToString();
        }

        public async Task<string> Couchbase_InsertWithExpirationAsync(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = await bucket.InsertAsync(document.Id, document.Content, 10);

            return result.Success.ToString();
        }

        public async Task<string> Couchbase_InsertReplicatePersistWithExpirationAsync(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = await bucket.InsertAsync(document.Id, document.Content, 10, ReplicateTo.One, PersistTo.One);

            return result.Success.ToString();
        }

        #endregion

        #region Upsert Methods

        public string Couchbase_Upsert()
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = "Couchbase_Upsert",
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Upsert(document.Id, document.Content);
            return result.Success.ToString();
        }

        public string Couchbase_UpsertDocument()
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = "Couchbase_UpsertDocument",
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Upsert(document);
            return result.Success.ToString();
        }

        public string Couchbase_UpsertCASWithExpiration()
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = "Couchbase_UpsertCASWithExpiration",
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Upsert(document.Id, document.Content, document.Cas, TimeSpan.FromSeconds(10));
            return result.Success.ToString();
        }

        public string Couchbase_UpsertReplicatePersist()
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = "Couchbase_UpsertReplicatePersist",
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Upsert(document.Id, document.Content, ReplicateTo.One, PersistTo.One);
            return result.Success.ToString();
        }

        public string Couchbase_UpsertReplicatePersistWithExpiration()
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = "Couchbase_UpsertReplicatePersistWithExpiration",
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Upsert(document.Id, document.Content, TimeSpan.FromSeconds(10), ReplicateTo.One, PersistTo.One);
            return result.Success.ToString();
        }

        public string Couchbase_UpsertCASReplicatePersistWithExpiration()
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = "Couchbase_UpsertCASReplicatePersistWithExpiration",
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Upsert(document.Id, document.Content, document.Cas, TimeSpan.FromSeconds(10), ReplicateTo.One, PersistTo.One);
            return result.Success.ToString();
        }

        public string Couchbase_UpsertMultiple()
        {
            var bucket = _connection.Bucket;
            var testobject = new CouchbaseTestObject() { Name = "New Relic" };

            var documentDictionary = new Dictionary<string, CouchbaseTestObject>()
            {
                {"Couchbase_UpsertMultiple_1", testobject},
                {"Couchbase_UpsertMultiple_2", testobject}
            };

            var result = bucket.Upsert(documentDictionary);
            return result.Count.ToString();
        }

        public string Couchbase_UpsertMultipleParallelOptions()
        {
            var bucket = _connection.Bucket;
            var testobject = new CouchbaseTestObject() { Name = "New Relic" };

            var documentDictionary = new Dictionary<string, CouchbaseTestObject>()
            {
                {"Couchbase_UpsertMultipleParallelOptions_1", testobject},
                {"Couchbase_UpsertMultipleParallelOptions_2", testobject}
            };

            var result = bucket.Upsert(documentDictionary, new ParallelOptions() { CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 4, TaskScheduler = TaskScheduler.Current });
            return result.Count.ToString();
        }

        public string Couchbase_UpsertMultipleParallelOptionsWithRangeSize()
        {
            var bucket = _connection.Bucket;
            var testobject = new CouchbaseTestObject() { Name = "New Relic" };

            var documentDictionary = new Dictionary<string, CouchbaseTestObject>()
            {
                {"Couchbase_UpsertMultipleParallelOptionsWithRangeSize_1", testobject},
                {"Couchbase_UpsertMultipleParallelOptionsWithRangeSize_2", testobject}
            };

            var result = bucket.Upsert(documentDictionary, new ParallelOptions() { CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 4, TaskScheduler = TaskScheduler.Current }, 10);
            return result.Count.ToString();
        }

        public async Task<string> Couchbase_UpsertAsync()
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = "Couchbase_UpsertAsync",
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = await bucket.UpsertAsync(document.Id, document.Content);
            return result.Success.ToString();
        }

        public async Task<string> Couchbase_UpsertCASWithExpirationAsync()
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = "Couchbase_UpsertCASWithExpirationAsync",
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = await bucket.UpsertAsync(document.Id, document.Content, document.Cas, TimeSpan.FromSeconds(10));
            return result.Success.ToString();
        }

        public async Task<string> Couchbase_UpsertCASReplicatePersistWithExpirationAsync()
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = "Couchbase_UpsertCASReplicatePersistWithExpirationAsync",
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = await bucket.UpsertAsync(document.Id, document.Content, document.Cas, TimeSpan.FromSeconds(10), ReplicateTo.One, PersistTo.One);
            return result.Success.ToString();
        }

        #endregion

        #region Remove Methods

        public string Couchbase_RemoveCAS(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId
            };

            var result = bucket.Remove(document.Id, document.Cas);

            return result.Success.ToString();
        }

        public string Couchbase_RemoveDocument(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId
            };

            var result = bucket.Remove(document);

            return result.Success.ToString();
        }

        public string Couchbase_RemoveReplicatePersist(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId
            };

            var result = bucket.Remove(document.Id, ReplicateTo.One, PersistTo.One);

            return result.Success.ToString();
        }

        public string Couchbase_RemoveCASReplicatePersist(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId
            };

            var result = bucket.Remove(document.Id, document.Cas, ReplicateTo.One, PersistTo.One);

            return result.Success.ToString();
        }

        public string Couchbase_RemoveMultiple(string documentId)
        {
            var bucket = _connection.Bucket;

            var result = bucket.Remove(new List<string> { documentId });

            return result.Count.ToString();
        }

        public string Couchbase_RemoveMultipleWithParallelOptions(string documentId)
        {
            var bucket = _connection.Bucket;

            var result = bucket.Remove(new List<string> { documentId }, new ParallelOptions() { CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 4, TaskScheduler = TaskScheduler.Current });

            return result.Count.ToString();
        }

        public string Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize(string documentId)
        {
            var bucket = _connection.Bucket;

            var result = bucket.Remove(new List<string> { documentId }, new ParallelOptions() { CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 4, TaskScheduler = TaskScheduler.Current }, 10);

            return result.Count.ToString();
        }

        public async Task<string> Couchbase_RemoveAsync(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId
            };

            var result = await bucket.RemoveAsync(document);

            return result.Success.ToString();
        }


        #endregion

        #region Replace Methods 

        public string Couchbase_Replace(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Replace(document.Id, document.Content);

            return result.Success.ToString();
        }

        public string Couchbase_ReplaceDocument(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Replace(document);

            return result.Success.ToString();
        }

        public string Couchbase_ReplaceCAS(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Replace(document.Id, document.Content, document.Cas);

            return result.Success.ToString();
        }

        public string Couchbase_ReplaceWithExpiration(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Replace(document.Id, document.Content, 10);

            return result.Success.ToString();
        }

        public string Couchbase_ReplaceCASWithExpiration(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Replace(document.Id, document.Content, document.Cas, 10);

            return result.Success.ToString();
        }

        public string Couchbase_ReplaceReplicatePersist(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Replace(document.Id, document.Content, ReplicateTo.One, PersistTo.One);

            return result.Success.ToString();
        }

        public string Couchbase_ReplaceCASReplicatePersist(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Replace(document.Id, document.Content, document.Cas, ReplicateTo.One, PersistTo.One);

            return result.Success.ToString();
        }

        public string Couchbase_ReplaceCASReplicatePersistWithExpiration(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = bucket.Replace(document.Id, document.Content, document.Cas, 10, ReplicateTo.One, PersistTo.One);

            return result.Success.ToString();
        }

        public async Task<string> Couchbase_ReplaceAsync(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId,
                Content = new CouchbaseTestObject() { Name = "New Relic" }
            };

            var result = await bucket.ReplaceAsync(document);

            return result.Success.ToString();
        }

        #endregion

        #region Query Methods

        public string Couchbase_Query()
        {
            var bucketName = CouchbaseTestObject.CouchbaseTestBucket;
            var bucket = _connection.Bucket;

            var result = bucket.Query<dynamic>("SELECT * FROM `" + bucketName + "` LIMIT 10");

            return result.Rows.Count().ToString();
        }

        public string Couchbase_QueryRequest()
        {
            var bucketName = CouchbaseTestObject.CouchbaseTestBucket;
            var bucket = _connection.Bucket;

            var queryRequest = new Couchbase.N1QL.QueryRequest("SELECT * FROM `" + bucketName + "` LIMIT 10");

            var result = bucket.Query<dynamic>(queryRequest);

            return result.Rows.Count().ToString();
        }

        public string Couchbase_SearchQuery()
        {
            var bucket = _connection.Bucket;
            var result = bucket.Query(new SearchQuery
            {
                Index = "idx_travel_content",
                Query = new MatchQuery("New Relic"),
                SearchParams = new SearchParams().Limit(10).Timeout(TimeSpan.FromMilliseconds(10000))
            });

            return result.TotalHits.ToString();
        }

        public string Couchbase_ViewQuery()
        {
            var bucket = _connection.Bucket;
            var viewQuery = new ViewQuery().Limit(10);
            var result = bucket.Query<dynamic>(viewQuery);

            return result.TotalRows.ToString();
        }

        public async Task<string> Couchbase_QueryAsync()
        {
            var bucketName = CouchbaseTestObject.CouchbaseTestBucket;
            var bucket = _connection.Bucket;

            var result = await bucket.QueryAsync<dynamic>("SELECT * FROM `" + bucketName + "` LIMIT 10");

            return result.Rows.Count().ToString();
        }

        public async Task<string> Couchbase_QueryRequestAsync()
        {
            var bucketName = CouchbaseTestObject.CouchbaseTestBucket;
            var bucket = _connection.Bucket;

            var queryRequest = new Couchbase.N1QL.QueryRequest("SELECT * FROM `" + bucketName + "` LIMIT 10");

            var result = await bucket.QueryAsync<QueryRequest>(queryRequest);

            return result.Rows.Count().ToString();
        }

        public async Task<string> Couchbase_SearchQueryAsync()
        {
            var bucket = _connection.Bucket;
            var result = await bucket.QueryAsync(new SearchQuery
            {
                Index = "idx_travel_content",
                Query = new MatchQuery("New Relic"),
                SearchParams = new SearchParams().Limit(10).Timeout(TimeSpan.FromMilliseconds(10000))
            });

            return result.TotalHits.ToString();
        }

        public async Task<string> Couchbase_ViewQueryAsync()
        {
            var bucket = _connection.Bucket;
            var viewQuery = new ViewQuery().Limit(10);
            var result = await bucket.QueryAsync<dynamic>(viewQuery);

            return result.TotalRows.ToString();
        }

        #endregion

        #region Misc Methods

        [HttpGet]
        public string Couchbase_Append()
        {
            var bucket = _connection.Bucket;
            var result = bucket.Append("New Relic", "New Relic Append");
            return result.Status.ToString();
        }

        [HttpGet]
        public string Couchbase_Prepend()
        {
            var bucket = _connection.Bucket;
            var result = bucket.Prepend("New Relic", "New Relic Perpend");
            return result.Status.ToString();
        }

        [HttpGet]
        public string Couchbase_Decrement()
        {
            var bucket = _connection.Bucket;
            var result = bucket.Decrement("New Relic");
            return result.Value.ToString();
        }

        [HttpGet]
        public string Couchbase_Increment()
        {
            var bucket = _connection.Bucket;
            var result = bucket.Increment("New Relic");
            return result.Value.ToString();
        }


        [HttpGet]
        public string Couchbase_Observe(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId
            };

            try
            {
                var result = bucket.Observe(document.Id, document.Cas, false, ReplicateTo.One, PersistTo.One);
                return result.ToJson();
            }
            catch (ReplicaNotConfiguredException e)
            {
                // This happens with the message: Not enough replicas configured on the bucket.
                return e.Message;
            }
        }

        [HttpGet]
        public string Couchbase_Touch(string documentId)
        {
            var bucket = _connection.Bucket;

            var result = bucket.Touch(documentId, TimeSpan.FromSeconds(5));

            return result.Success.ToString();
        }

        [HttpGet]
        public string Couchbase_Unlock(string documentId)
        {
            var bucket = _connection.Bucket;

            var document = new Document<CouchbaseTestObject>()
            {
                Id = documentId
            };

            var result = bucket.Unlock(document.Id, document.Cas);

            return result.Success.ToString();
        }

        //Invoke

        [HttpGet]
        public string Couchbase_Invoke()
        {
            var bucket = _connection.Bucket as CouchbaseBucket;
            bool result = true;
            try
            {
                if (bucket != null)
                {
                    var task = bucket.Invoke<dynamic>(bucket.MutateIn<dynamic>(_documentId));
                    result = task.Success;
                }

            }
            catch (Exception e)
            {
                return e.Message;
            }

            return result.ToString();
        }

        public async Task<string> Couchbase_InvokeAsync()
        {
            var bucket = _connection.Bucket as CouchbaseBucket;
            bool result = true;
            try
            {
                if (bucket != null)
                {
                    var task = await bucket.InvokeAsync<dynamic>(bucket.MutateIn<dynamic>(_documentId));
                    result = task.Success;
                }

            }
            catch (Exception e)
            {
                return e.Message;
            }

            return result.ToString();
        }

        //Exists
        [HttpGet]
        public string Couchbase_Exists()
        {
            var bucket = _connection.Bucket;
            var result = bucket.Exists(_documentId);
            return result.ToString();
        }

        [HttpGet]
        public async Task<string> Couchbase_ExistsAsync()
        {
            var bucket = _connection.Bucket;
            var result = await bucket.ExistsAsync(_documentId);
            return result.ToString();
        }
        #endregion

        private class CouchbaseConnection : IDisposable
        {
            private ICluster _cluster;

            public IBucket Bucket { get; private set; }

            public void Connect()
            {
                var config = GetConnectionConfig();
                _cluster = new Cluster(config);
                Bucket = _cluster.OpenBucket(CouchbaseTestObject.CouchbaseTestBucket);
            }

            public void Disconnect()
            {
                _cluster.CloseBucket(Bucket);
                Bucket.Dispose();
                Bucket = null;
                _cluster.Dispose();
                _cluster = null;
            }

            private ClientConfiguration GetConnectionConfig()
            {
                var config = new ClientConfiguration();
                config.Servers = new List<Uri>()
            {
                new Uri(CouchbaseTestObject.CouchbaseServerUrl)
            };
                config.UseSsl = false;
                return config;
            }

            public void Dispose()
            {
                Disconnect();
            }
        }
    }
}
