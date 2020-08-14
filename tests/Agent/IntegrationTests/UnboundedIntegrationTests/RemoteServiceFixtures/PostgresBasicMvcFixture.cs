// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class PostgresBasicMvcFixture : RemoteApplicationFixture
    {
        public PostgresBasicMvcFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Unbounded))
        {
        }

        public void GetPostgres()
        {
            var address = $"http://{DestinationServerName}:{Port}/Postgres/Postgres";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetPostgresAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Postgres/PostgresAsync";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresParameterizedStoredProcedure(string procedureName)
        {
            var address = $"http://{DestinationServerName}:{Port}/Postgres/PostgresParameterizedStoredProcedure?procedureName={procedureName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresParameterizedStoredProcedureAsync(string procedureName)
        {
            var address = $"http://{DestinationServerName}:{Port}/Postgres/PostgresParameterizedStoredProcedureAsync?procedureName={procedureName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresExecuteScalar()
        {
            var address = $"http://{DestinationServerName}:{Port}/Postgres/PostgresExecuteScalar";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresExecuteScalarAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Postgres/PostgresExecuteScalarAsync";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresIteratorTest()
        {
            var address = $"http://{DestinationServerName}:{Port}/Postgres/PostgresIteratorTest";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void PostgresAsyncIteratorTest()
        {
            var address = $"http://{DestinationServerName}:{Port}/Postgres/PostgresAsyncIteratorTest";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }
    }
}
