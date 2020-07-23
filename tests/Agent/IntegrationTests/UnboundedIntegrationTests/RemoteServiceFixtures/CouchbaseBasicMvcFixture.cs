using System;
using System.Collections.Generic;
using System.Net;
using Couchbase;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Couchbase;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
	public class CouchbaseBasicMvcFixture : RemoteApplicationFixture
	{
		private readonly CouchbaseConnection _connection;

		public CouchbaseBasicMvcFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Unbounded))
		{
			_connection = new CouchbaseConnection();
			_connection.Connect();
		}

		#region CouchbaseController Actions

		#region CouchbaseController GetActions

		public void Couchbase_Get()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Get";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}
		public void Couchbase_GetMultiple()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetMultiple";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetMultipleParallelOptions()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetMultipleParallelOptions";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetMultipleParallelOptionsWithRangeSize()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetMultipleParallelOptionsWithRangeSize";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetAndTouch()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetAndTouch?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetWithLock()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetWithLock";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetDocument()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetDocument";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetFromReplica()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetFromReplica";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController GetAsyncActions

		public void Couchbase_GetAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetAndTouchAsync()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetAndTouchAsync?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetDocumentAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetDocumentAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetFromReplicaAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetFromReplicaAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_GetWithLockAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetWithLockAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController InsertActions

		public void Couchbase_Insert()
		{
			var documentId = GenerateDocumentId();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Insert?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_InsertDocument()
		{
			var documentId = GenerateDocumentId();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertDocument?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_InsertWithExpiration()
		{
			var documentId = GenerateDocumentId();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertWithExpiration?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_InsertReplicatePersist()
		{
			var documentId = GenerateDocumentId();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertReplicatePersist?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_InsertReplicatePersistWithExpiration()
		{
			var documentId = GenerateDocumentId();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertReplicatePersistWithExpiration?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController InsertAsyncActions

		public void Couchbase_InsertAsync()
		{
			var documentId = GenerateDocumentId();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertAsync?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}
		
		public void Couchbase_InsertWithExpirationAsync()
		{
			var documentId = GenerateDocumentId();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertWithExpirationAsync?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_InsertReplicatePersistAsync()
		{
			var documentId = GenerateDocumentId();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertReplicatePersistAsync?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}
		
		public void Couchbase_InsertReplicatePersistWithExpirationAsync()
		{
			var documentId = GenerateDocumentId();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertReplicatePersistWithExpirationAsync?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController UpsertActions 

		public void Couchbase_Upsert()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Upsert";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_UpsertCASWithExpiration()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertCASWithExpiration";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_UpsertReplicatePersist()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertReplicatePersist";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_UpsertReplicatePersistWithExpiration()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertReplicatePersistWithExpiration";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_UpsertCASReplicatePersistWithExpiration()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertCASReplicatePersistWithExpiration";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_UpsertMultiple()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertMultiple";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_UpsertMultipleParallelOptions()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertMultipleParallelOptions";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_UpsertMultipleParallelOptionsWithRangeSize()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertMultipleParallelOptionsWithRangeSize";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController UpsertAsyncActions

		public void Couchbase_UpsertAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_UpsertDocument()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertDocument";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}

		}

		public void Couchbase_UpsertCASWithExpirationAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertCASWithExpirationAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_UpsertCASReplicatePersistWithExpirationAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertCASReplicatePersistWithExpirationAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController RemoveActions

		public void Couchbase_RemoveCAS()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveCAS?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_RemoveDocument()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveDocument?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_RemoveReplicatePersist()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveReplicatePersist?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_RemoveCASReplicatePersist()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveCASReplicatePersist?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_RemoveMultiple()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveMultiple?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_RemoveMultipleWithParallelOptions()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveMultipleWithParallelOptions?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}


		#endregion

		#region CouchbaseController RemoveAsyncActions

		public void Couchbase_RemoveAsync()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveAsync?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController ReplaceActions

		public void Couchbase_Replace()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Replace?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_ReplaceDocument()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceDocument?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_ReplaceCAS()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceCAS?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_ReplaceWithExpiration()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceWithExpiration?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_ReplaceCASWithExpiration()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceCASWithExpiration?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_ReplaceReplicatePersist()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceReplicatePersist?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_ReplaceCASReplicatePersist()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceCASReplicatePersist?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_ReplaceCASReplicatePersistWithExpiration()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceCASReplicatePersistWithExpiration?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController ReplaceAsyncActions

		public void Couchbase_ReplaceAsync()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceAsync?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController QueryActions

		public void Couchbase_Query()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Query";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseControll QueryAsyncActions

		public void Couchbase_QueryAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_QueryAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController MiscActions

		public void Couchbase_Append()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Append";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_Prepend()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Prepend";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_Decrement()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Decrement";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_Increment()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Increment";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_Observe()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Observe?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_Touch()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Touch?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_Unlock()
		{
			var documentId = InsertDocument();

			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Unlock?documentId={documentId}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_Exists()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Exists";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_Invoke()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Invoke";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion

		#region CouchbaseController MiscAsyncActions

		public void Couchbase_ExistsAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ExistsAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void Couchbase_InvokeAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InvokeAsync";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		#endregion


		#endregion

		private readonly IList<String> _documentIds = new List<String>();

		private void RemoveDocuments()
		{
			foreach (var documentId in _documentIds)
			{
				_connection.Bucket.Remove(documentId);
			}
		}

		private String GenerateDocumentId()
		{
			var documentId = $"integrationTestDocumentId-{Guid.NewGuid().ToString("N")}";
			_documentIds.Add(documentId);
			return documentId;
		}

		private String InsertDocument()
		{
			var documentId = GenerateDocumentId();

			var document = new Document<CouchbaseTestObject>()
			{
				Id = documentId,
				Content = new CouchbaseTestObject { Name = "New Relic" }
			};

			var result = _connection.Bucket.Insert(document.Id, document.Content);

			if (!result.Success)
				throw new Exception("Inserting a document for the test setup failed.");

			return documentId;
		}

		public override void Dispose()
		{
			base.Dispose();
			RemoveDocuments();
			_connection.Dispose();
		}
	}
}
