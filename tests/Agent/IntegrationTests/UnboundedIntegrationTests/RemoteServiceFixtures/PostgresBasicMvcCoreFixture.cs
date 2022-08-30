// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class PostgresBasicMvcCoreFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"BasicMvcCoreApplication";
        private const string ExecutableName = "BasicMvcCoreApplication.exe";

        public PostgresBasicMvcCoreFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Unbounded, createsPidFile: true, isCoreApp: true, publishApp: true))
        {
        }

        public void GetPostgres()
        {
            var address = $"http://127.0.0.1:{Port}/Postgres/Postgres";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetPostgresAsync()
        {
            var address = $"http://127.0.0.1:{Port}/Postgres/PostgresAsync";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresParameterizedStoredProcedure(string procedureName)
        {
            var address = $"http://127.0.0.1:{Port}/Postgres/PostgresParameterizedStoredProcedure?procedureName={procedureName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresParameterizedStoredProcedureAsync(string procedureName)
        {
            var address = $"http://127.0.0.1:{Port}/Postgres/PostgresParameterizedStoredProcedureAsync?procedureName={procedureName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresExecuteScalar()
        {
            var address = $"http://127.0.0.1:{Port}/Postgres/PostgresExecuteScalar";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresExecuteScalarAsync()
        {
            var address = $"http://127.0.0.1:{Port}/Postgres/PostgresExecuteScalarAsync";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresIteratorTest()
        {
            var address = $"http://127.0.0.1:{Port}/Postgres/PostgresIteratorTest";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresAsyncIteratorTest()
        {
            var address = $"http://127.0.0.1:{Port}/Postgres/PostgresAsyncIteratorTest";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }
    }
}
