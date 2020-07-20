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

namespace BasicMvcApplication.Controllers
{
	public class CouchbaseController : Controller
	{
		private readonly String[] _documentIds = new String[] { "airline_10765", "route_5639" };
		private const String _documentId = "airline_10765";
		private readonly CouchbaseConnection _connection;

		public CouchbaseController()
		{
			_connection = new CouchbaseConnection();
			_connection.Connect();
		}

		protected override void Dispose(Boolean disposing)
		{
			_connection.Dispose();
			base.Dispose(disposing);
		}

		#region Get Methods

		[HttpGet]
		public String Couchbase_Get()
		{
			var bucket = _connection.Bucket;
			var doc = bucket.Get<dynamic>(_documentId).Value.ToString();
			return doc;
		}

		[HttpGet]
		public String Couchbase_GetMultiple()
		{
			var bucket = _connection.Bucket;
			var docCount = bucket.Get<dynamic>(_documentIds).Values.Count;
			return docCount.ToString();
		}

		[HttpGet]
		public String Couchbase_GetMultipleParallelOptions()
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
		public String Couchbase_GetMultipleParallelOptionsWithRangeSize()
		{
			var bucket = _connection.Bucket;

			var parallelOptions = new ParallelOptions() {
															CancellationToken = CancellationToken.None,
															MaxDegreeOfParallelism = 1,
															TaskScheduler = TaskScheduler.Current
														};

			var docCount = bucket.Get<dynamic>(_documentIds, parallelOptions, 10).Values.Count;
			return docCount.ToString();
		}

		[HttpGet]
		public String Couchbase_GetAndTouch(String documentId)
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
		public String Couchbase_GetWithLock()
		{
			var bucket = _connection.Bucket;
			var doc = bucket.GetAndLock<dynamic>(_documentId, 5).ToJson();
			return doc;
		}

		[HttpGet]
		public String Couchbase_GetDocument()
		{
			var bucket = _connection.Bucket;
			var doc = bucket.GetDocument<dynamic>(_documentId).ToJson();
			return doc;
		}

		[HttpGet]
		public String Couchbase_GetFromReplica()
		{
			var bucket = _connection.Bucket;
			var doc = bucket.GetFromReplica<dynamic>(_documentId).ToJson();
			return doc;
		}

		[HttpGet]
		public async Task<String> Couchbase_GetAsync()
		{
			var bucket = _connection.Bucket;
			var doc = await bucket.GetAsync<dynamic>(_documentId);
			return doc.ToJson();
		}

		[HttpGet]
		public async Task<String> Couchbase_GetAndTouchAsync(String documentId)
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
		public async Task<String> Couchbase_GetDocumentAsync()
		{
			var bucket = _connection.Bucket;
			var doc = await bucket.GetDocumentAsync<dynamic>(_documentId);
			return doc.ToJson();

		}

		[HttpGet]
		public async Task<String> Couchbase_GetFromReplicaAsync()
		{
			var bucket = _connection.Bucket;
			var doc = await bucket.GetFromReplicaAsync<dynamic>(_documentId);
			return doc.ToJson();
		}

		[HttpGet]
		public async Task<String> Couchbase_GetWithLockAsync()
		{
			var bucket = _connection.Bucket;
			var doc = await bucket.GetAndLockAsync<dynamic>(_documentId, TimeSpan.FromSeconds(5));
			return doc.ToJson();
		}

		#endregion

		#region Insert Methods
		[HttpGet]
		public String Couchbase_Insert(String documentId)
		{
			var bucket = _connection.Bucket;

			var document = new Document<CouchbaseTestObject>()
			{
				Id = documentId,
				Content = new CouchbaseTestObject(){Name = "New Relic"}
			};

			var result = bucket.Insert(document.Id, document.Content);
			return result.Success.ToString();
		}

		[HttpGet]
		public String Couchbase_InsertDocument(String documentId)
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
		public String Couchbase_InsertWithExpiration(String documentId)
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
		public String Couchbase_InsertReplicatePersist(String documentId)
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
		public String Couchbase_InsertReplicatePersistWithExpiration(String documentId)
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
		public async Task<String> Couchbase_InsertAsync(String documentId)
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

		public async Task<String> Couchbase_InsertReplicatePersistAsync(String documentId)
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

		public async Task<String> Couchbase_InsertWithExpirationAsync(String documentId)
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

		public async Task<String> Couchbase_InsertReplicatePersistWithExpirationAsync(String documentId)
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

		public String Couchbase_Upsert()
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

		public String Couchbase_UpsertDocument()
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

		public String Couchbase_UpsertCASWithExpiration()
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

		public String Couchbase_UpsertReplicatePersist()
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

		public String Couchbase_UpsertReplicatePersistWithExpiration()
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

		public String Couchbase_UpsertCASReplicatePersistWithExpiration()
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

		public String Couchbase_UpsertMultiple()
		{
			var bucket = _connection.Bucket;
			var testObject = new CouchbaseTestObject() { Name = "New Relic"};

			var documentDictionary = new Dictionary<string, CouchbaseTestObject>()
			{
				{"Couchbase_UpsertMultiple_1", testObject},
				{"Couchbase_UpsertMultiple_2", testObject}
			};

			var result = bucket.Upsert(documentDictionary);
			return result.Count.ToString();
		}

		public String Couchbase_UpsertMultipleParallelOptions()
		{
			var bucket = _connection.Bucket;
			var testObject = new CouchbaseTestObject() { Name = "New Relic" };

			var documentDictionary = new Dictionary<string, CouchbaseTestObject>()
			{
				{"Couchbase_UpsertMultipleParallelOptions_1", testObject},
				{"Couchbase_UpsertMultipleParallelOptions_2", testObject}
			};

			var result = bucket.Upsert(documentDictionary, new ParallelOptions() { CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 4, TaskScheduler = TaskScheduler.Current});
			return result.Count.ToString();
		}

		public String Couchbase_UpsertMultipleParallelOptionsWithRangeSize()
		{
			var bucket = _connection.Bucket;
			var testObject = new CouchbaseTestObject() { Name = "New Relic" };

			var documentDictionary = new Dictionary<string, CouchbaseTestObject>()
			{
				{"Couchbase_UpsertMultipleParallelOptionsWithRangeSize_1", testObject},
				{"Couchbase_UpsertMultipleParallelOptionsWithRangeSize_2", testObject}
			};

			var result = bucket.Upsert(documentDictionary, new ParallelOptions() { CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 4, TaskScheduler = TaskScheduler.Current }, 10);
			return result.Count.ToString();
		}

		public async Task<String> Couchbase_UpsertAsync()
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

		public async Task<String> Couchbase_UpsertCASWithExpirationAsync()
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

		public async Task<String> Couchbase_UpsertCASReplicatePersistWithExpirationAsync()
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

		public String Couchbase_RemoveCAS(String documentId)
		{
			var bucket = _connection.Bucket;

			var document = new Document<CouchbaseTestObject>()
			{
				Id = documentId
			};

			var result = bucket.Remove(document.Id, document.Cas);
			
			return result.Success.ToString();
		}

		public String Couchbase_RemoveDocument(String documentId)
		{
			var bucket = _connection.Bucket;

			var document = new Document<CouchbaseTestObject>()
			{
				Id = documentId
			};

			var result = bucket.Remove(document);

			return result.Success.ToString();
		}

		public String Couchbase_RemoveReplicatePersist(String documentId)
		{
			var bucket = _connection.Bucket;

			var document = new Document<CouchbaseTestObject>()
			{
				Id = documentId
			};

			var result = bucket.Remove(document.Id, ReplicateTo.One, PersistTo.One);

			return result.Success.ToString();
		}

		public String Couchbase_RemoveCASReplicatePersist(String documentId)
		{
			var bucket = _connection.Bucket;

			var document = new Document<CouchbaseTestObject>()
			{
				Id = documentId
			};

			var result = bucket.Remove(document.Id, document.Cas, ReplicateTo.One, PersistTo.One);

			return result.Success.ToString();
		}

		public String Couchbase_RemoveMultiple(String documentId)
		{
			var bucket = _connection.Bucket;

			var result = bucket.Remove(new List<String> { documentId });

			return result.Count.ToString();
		}

		public String Couchbase_RemoveMultipleWithParallelOptions(String documentId)
		{
			var bucket = _connection.Bucket;

			var result = bucket.Remove(new List<String> { documentId }, new ParallelOptions() { CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 4, TaskScheduler = TaskScheduler.Current});

			return result.Count.ToString();
		}

		public String Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize(String documentId)
		{
			var bucket = _connection.Bucket;

			var result = bucket.Remove(new List<String> { documentId }, new ParallelOptions() { CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 4, TaskScheduler = TaskScheduler.Current }, 10);

			return result.Count.ToString();
		}

		public async Task<String> Couchbase_RemoveAsync(String documentId)
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

		public String Couchbase_Replace(String documentId)
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

		public String Couchbase_ReplaceDocument(String documentId)
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

		public String Couchbase_ReplaceCAS(String documentId)
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

		public String Couchbase_ReplaceWithExpiration(String documentId)
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

		public String Couchbase_ReplaceCASWithExpiration(String documentId)
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

		public String Couchbase_ReplaceReplicatePersist(String documentId)
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

		public String Couchbase_ReplaceCASReplicatePersist(String documentId)
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

		public String Couchbase_ReplaceCASReplicatePersistWithExpiration(String documentId)
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

		public async Task<String> Couchbase_ReplaceAsync(String documentId)
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

		public String Couchbase_Query()
		{
			var bucketName = CouchbaseTestObject.CouchbaseTestBucket;
			var bucket = _connection.Bucket;

			var result = bucket.Query<dynamic>("SELECT * FROM `" + bucketName + "` LIMIT 10");

			return result.Rows.Count().ToString();
		}

		public async Task<String> Couchbase_QueryAsync()
		{
			var bucketName = CouchbaseTestObject.CouchbaseTestBucket;
			var bucket = _connection.Bucket;

			var result = await bucket.QueryAsync<dynamic>("SELECT * FROM `" + bucketName + "` LIMIT 10");

			return result.Rows.Count().ToString();
		}

		#endregion

		#region Misc Methods

		[HttpGet]
		public String Couchbase_Append()
		{
			var bucket = _connection.Bucket;
			var result = bucket.Append("New Relic", "New Relic Append");
			return result.Status.ToString();
		}

		[HttpGet]
		public String Couchbase_Prepend()
		{
			var bucket = _connection.Bucket;
			var result = bucket.Prepend("New Relic", "New Relic Perpend");
			return result.Status.ToString();
		}

		[HttpGet]
		public String Couchbase_Decrement()
		{
			var bucket = _connection.Bucket;
			var result = bucket.Decrement("New Relic");
			return result.Value.ToString();
		}

		[HttpGet]
		public String Couchbase_Increment()
		{
			var bucket = _connection.Bucket;
			var result = bucket.Increment("New Relic");
			return result.Value.ToString();
		}


		[HttpGet]
		public String Couchbase_Observe(String documentId)
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
		public String Couchbase_Touch(String documentId)
		{
			var bucket = _connection.Bucket;

			var result = bucket.Touch(documentId, TimeSpan.FromSeconds(5));

			return result.Success.ToString();
		}

		[HttpGet]
		public String Couchbase_Unlock(String documentId)
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
		public String Couchbase_Invoke()
		{
			var bucket = _connection.Bucket as CouchbaseBucket;
			bool result = true;
			try
			{
				if (bucket != null)
				{
					var task =  bucket.Invoke<dynamic>(bucket.MutateIn<dynamic>(_documentId));
					result = task.Success;
				}

			}
			catch (Exception e)
			{
				return e.Message;
			}

			return result.ToString();
		}

		public async Task<String> Couchbase_InvokeAsync()
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
		public String Couchbase_Exists()
		{
			var bucket = _connection.Bucket;
			var result = bucket.Exists(_documentId);
			return result.ToString();
		}

		[HttpGet]
		public async Task<String> Couchbase_ExistsAsync()
		{
			var bucket = _connection.Bucket;
			var result = await bucket.ExistsAsync(_documentId);
			return result.ToString();
		}
		#endregion
	}
}