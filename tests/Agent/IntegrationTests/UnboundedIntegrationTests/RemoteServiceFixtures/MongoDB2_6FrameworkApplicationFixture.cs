// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using Xunit;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class MongoDB2_6ApplicationFixture : RemoteApplicationFixture
    {
        private readonly string _baseUrl;
        private const string MongoCollectionPath = "/api/MongoDB";
        private readonly string _deleteDatabaseUrl;

        public MongoDB2_6ApplicationFixture(bool isCore) : base(isCore ? new RemoteService("MongoDB2_6CoreApplication", "MongoDB2_6CoreApplication.exe", "net5", ApplicationType.Unbounded, isCoreApp: true, publishApp: true) :
            new RemoteWebApplication("MongoDB2_6Application", ApplicationType.Unbounded))
        {
            _baseUrl = $"http://localhost:{Port}";
            _deleteDatabaseUrl = $"{_baseUrl}{MongoCollectionPath}/DropDatabase?dbName=";
        }

        #region BasicCollectionMethods

        public void InsertOne()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/InsertOne/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void InsertOneAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/InsertOneAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void InsertMany()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/InsertMany/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void InsertManyAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/InsertManyAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void ReplaceOne()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/ReplaceOne/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void ReplaceOneAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/ReplaceOneAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void UpdateOne()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/UpdateOne/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void UpdateOneAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/UpdateOneAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void UpdateMany()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/UpdateMany/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void UpdateManyAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/UpdateManyAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DeleteOne()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DeleteOne/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DeleteOneAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DeleteOneAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DeleteMany()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DeleteMany/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DeleteManyAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DeleteManyAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void FindSync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/FindSync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void FindAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/FindAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void FindOneAndDelete()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/FindOneAndDelete/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void FindOneAndDeleteAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/FindOneAndDeleteAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void FindOneAndReplace()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/FindOneAndReplace/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void FindOneAndReplaceAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/FindOneAndReplaceAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void FindOneAndUpdate()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/FindOneAndUpdate/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void FindOneAndUpdateAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/FindOneAndUpdateAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void BulkWrite()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/BulkWrite/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void BulkWriteAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/BulkWriteAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void Aggregate()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/Aggregate/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void Count()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/Count/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void CountAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/CountAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void Distinct()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/Distinct/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DistinctAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DistinctAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void MapReduce()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/MapReduce/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void MapReduceAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/MapReduceAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void Watch()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/Watch/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void WatchAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/WatchAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        #endregion

        #region DatabaseMethods

        public void CreateCollection()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/CreateCollection/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void CreateCollectionAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/CreateCollectionAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DropCollection()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DropCollection/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DropCollectionAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DropCollectionAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void ListCollections()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/ListCollections/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void ListCollectionsAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/ListCollectionsAsync/{dbParam}";
            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void RenameCollection()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/RenameCollection/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void RenameCollectionAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/RenameCollectionAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void RunCommand()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/RunCommand/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void RunCommandAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/RunCommandAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        #endregion

        #region IndexManager Methods

        public void CreateOne()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/CreateOne/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void CreateOneAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/CreateOneAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void CreateMany()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/CreateMany/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void CreateManyAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/CreateManyAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }
        public void DropAll()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DropAll/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DropAllAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DropAllAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DropOne()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DropOne/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void DropOneAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/DropOneAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void List()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/List/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void ListAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/ListAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        #endregion

        #region MongoQueryProvider

        public void ExecuteModel()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/ExecuteModel/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void ExecuteModelAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/ExecuteModelAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        #endregion

        #region AsyncCursor

        public void MoveNext()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/GetNextBatch/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        public void MoveNextAsync()
        {
            var guid = Guid.NewGuid();
            var dbParam = $"?dbName={guid}";
            var address = $"{_baseUrl}{MongoCollectionPath}/GetNextBatchAsync/{dbParam}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                var deleteDatabaseResponse = webClient.DownloadString($"{_deleteDatabaseUrl}{guid}");
                Assert.NotNull(responseBody);
            }
        }

        #endregion

    }

    public class MongoDB2_6FrameworkApplicationFixture : MongoDB2_6ApplicationFixture
    {
        public MongoDB2_6FrameworkApplicationFixture() : base(false)
        {

        }
    }

    public class MongoDB2_6CoreApplicationFixture : MongoDB2_6ApplicationFixture
    {
        public MongoDB2_6CoreApplicationFixture() : base(true)
        {

        }
    }
}
