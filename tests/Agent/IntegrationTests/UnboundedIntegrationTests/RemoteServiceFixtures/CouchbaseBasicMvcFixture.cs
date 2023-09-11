// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Couchbase;

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

            GetStringAndAssertIsNotNull(address);
        }
        public void Couchbase_GetMultiple()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetMultiple";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetMultipleParallelOptions()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetMultipleParallelOptions";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetMultipleParallelOptionsWithRangeSize()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetMultipleParallelOptionsWithRangeSize";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetAndTouch()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetAndTouch?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetWithLock()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetWithLock";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetDocument()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetDocument";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetFromReplica()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetFromReplica";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController GetAsyncActions

        public void Couchbase_GetAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetAsync";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetAndTouchAsync()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetAndTouchAsync?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetDocumentAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetDocumentAsync";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetFromReplicaAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetFromReplicaAsync";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_GetWithLockAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_GetWithLockAsync";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController InsertActions

        public void Couchbase_Insert()
        {
            var documentId = GenerateDocumentId();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Insert?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_InsertDocument()
        {
            var documentId = GenerateDocumentId();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertDocument?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_InsertWithExpiration()
        {
            var documentId = GenerateDocumentId();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertWithExpiration?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_InsertReplicatePersist()
        {
            var documentId = GenerateDocumentId();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertReplicatePersist?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_InsertReplicatePersistWithExpiration()
        {
            var documentId = GenerateDocumentId();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertReplicatePersistWithExpiration?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController InsertAsyncActions

        public void Couchbase_InsertAsync()
        {
            var documentId = GenerateDocumentId();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertAsync?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_InsertWithExpirationAsync()
        {
            var documentId = GenerateDocumentId();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertWithExpirationAsync?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_InsertReplicatePersistAsync()
        {
            var documentId = GenerateDocumentId();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertReplicatePersistAsync?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_InsertReplicatePersistWithExpirationAsync()
        {
            var documentId = GenerateDocumentId();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InsertReplicatePersistWithExpirationAsync?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController UpsertActions 

        public void Couchbase_Upsert()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Upsert";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_UpsertCASWithExpiration()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertCASWithExpiration";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_UpsertReplicatePersist()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertReplicatePersist";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_UpsertReplicatePersistWithExpiration()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertReplicatePersistWithExpiration";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_UpsertCASReplicatePersistWithExpiration()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertCASReplicatePersistWithExpiration";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_UpsertMultiple()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertMultiple";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_UpsertMultipleParallelOptions()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertMultipleParallelOptions";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_UpsertMultipleParallelOptionsWithRangeSize()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertMultipleParallelOptionsWithRangeSize";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController UpsertAsyncActions

        public void Couchbase_UpsertAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertAsync";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_UpsertDocument()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertDocument";

            GetStringAndAssertIsNotNull(address);

        }

        public void Couchbase_UpsertCASWithExpirationAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertCASWithExpirationAsync";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_UpsertCASReplicatePersistWithExpirationAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_UpsertCASReplicatePersistWithExpirationAsync";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController RemoveActions

        public void Couchbase_RemoveCAS()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveCAS?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_RemoveDocument()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveDocument?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_RemoveReplicatePersist()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveReplicatePersist?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_RemoveCASReplicatePersist()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveCASReplicatePersist?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_RemoveMultiple()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveMultiple?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_RemoveMultipleWithParallelOptions()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveMultipleWithParallelOptions?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }


        #endregion

        #region CouchbaseController RemoveAsyncActions

        public void Couchbase_RemoveAsync()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_RemoveAsync?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController ReplaceActions

        public void Couchbase_Replace()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Replace?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_ReplaceDocument()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceDocument?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_ReplaceCAS()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceCAS?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_ReplaceWithExpiration()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceWithExpiration?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_ReplaceCASWithExpiration()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceCASWithExpiration?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_ReplaceReplicatePersist()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceReplicatePersist?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_ReplaceCASReplicatePersist()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceCASReplicatePersist?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_ReplaceCASReplicatePersistWithExpiration()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceCASReplicatePersistWithExpiration?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController ReplaceAsyncActions

        public void Couchbase_ReplaceAsync()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ReplaceAsync?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController QueryActions

        public void Couchbase_Query()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Query";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_QueryRequest()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_QueryRequest";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_SearchQuery()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_SearchQuery";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_ViewQuery()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ViewQuery";

            GetStringAndAssertIsNotNull(address);
        }
        #endregion

        #region CouchbaseControll QueryAsyncActions

        public void Couchbase_QueryAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_QueryAsync";

            GetStringAndAssertIsNotNull(address);
        }
        public void Couchbase_QueryRequestAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_QueryRequestAsync";

            GetStringAndAssertIsNotNull(address);
        }
        public void Couchbase_SearchQueryAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_SearchQueryAsync";

            GetStringAndAssertIsNotNull(address);
        }
        public void Couchbase_ViewQueryAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ViewQueryAsync";

            GetStringAndAssertIsNotNull(address);
        }
        #endregion

        #region CouchbaseController MiscActions

        public void Couchbase_Append()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Append";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_Prepend()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Prepend";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_Decrement()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Decrement";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_Increment()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Increment";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_Observe()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Observe?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_Touch()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Touch?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_Unlock()
        {
            var documentId = InsertDocument();

            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Unlock?documentId={documentId}";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_Exists()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Exists";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_Invoke()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_Invoke";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion

        #region CouchbaseController MiscAsyncActions

        public void Couchbase_ExistsAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_ExistsAsync";

            GetStringAndAssertIsNotNull(address);
        }

        public void Couchbase_InvokeAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Couchbase/Couchbase_InvokeAsync";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion


        #endregion

        private readonly IList<string> _documentIds = new List<string>();

        private void RemoveDocuments()
        {
            foreach (var documentId in _documentIds)
            {
                _connection.Bucket.Remove(documentId);
            }
        }

        private string GenerateDocumentId()
        {
            var documentId = $"integrationTestDocumentId-{Guid.NewGuid().ToString("N")}";
            _documentIds.Add(documentId);
            return documentId;
        }

        private string InsertDocument()
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
