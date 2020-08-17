// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
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

        public void GetStackExchangeRedis()
        {
            var address = $"http://{DestinationServerName}:{Port}/Redis/StackExchangeRedis";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetStackExchangeRedisStrongName()
        {
            var address = $"http://{DestinationServerName}:{Port}/Redis/StackExchangeRedisStrongName";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }


        public void GetStackExchangeRedisAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Redis/StackExchangeRedisAsync";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetStackExchangeRedisAsyncStrongName()
        {
            var address = $"http://{DestinationServerName}:{Port}/Redis/StackExchangeRedisAsyncStrongName";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

    }
}
