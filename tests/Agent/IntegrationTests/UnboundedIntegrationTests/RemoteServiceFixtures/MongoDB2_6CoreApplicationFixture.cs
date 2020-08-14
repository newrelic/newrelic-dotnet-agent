// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using Xunit;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class MongoDB2_6CoreApplicationFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"MongoDB2_6CoreApplication";
        private const string ExecutableName = "MongoDB2_6CoreApplication.exe";

        private readonly string _baseUrl;
        private const string MongoCollectionPath = "/api/MongoDB";
        private readonly string _deleteDatabaseUrl;


        public MongoDB2_6CoreApplicationFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Unbounded, isCoreApp: true, publishApp: true))
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

    }
}
