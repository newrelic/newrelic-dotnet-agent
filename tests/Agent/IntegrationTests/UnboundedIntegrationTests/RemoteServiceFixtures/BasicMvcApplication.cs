// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class BasicMvcApplication : RemoteApplicationFixture
    {
        public const string ExpectedTransactionName = @"WebTransaction/MVC/DefaultController/Index";

        public BasicMvcApplication() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Unbounded))
        {
        }

        #region DefaultController Actions

        public void GetMySql()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/MySql";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMySqlAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/MySqlAsync";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetPostgres()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/Postgres";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetPostgresAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/PostgresAsync";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetStackExchangeRedis()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/StackExchangeRedis";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetStackExchangeRedisStrongName()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/StackExchangeRedisStrongName";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        #endregion
    }
}
