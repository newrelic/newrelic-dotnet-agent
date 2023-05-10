// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net.Http;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreFeaturesFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCoreFeatures";
        private const string ExecutableName = @"AspNetCoreFeatures.exe";
        public AspNetCoreFeaturesFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, targetFramework: "net7.0", ApplicationType.Bounded, true, true, true))
        {
        }

        public void Get()
        {
            var address = $"http://localhost:{Port}/";
            DownloadStringAndAssertContains(address, "<html>");
        }

        public void ThrowException()
        {
            var address = $"http://localhost:{Port}/Home/ThrowException";
            using (var httpClient = new HttpClient())
            {
                Assert.Throws<AggregateException>(() => httpClient.GetStringAsync(address).Wait());
            }
        }

        public void AccessCollectible()
        {
            var address = $"http://localhost:{Port}/Collectible/AccessCollectible";
            using (var httpClient = new HttpClient())
            {
                httpClient.GetStringAsync(address).Wait();
            }
        }

        public void AsyncStream()
        {
            var address = $"http://localhost:{Port}/api/AsyncStream";
            DownloadStringAndAssertContains(address, "45");
        }

        public void InterfaceDefaultsGetWithAttributes()
        {
            var address = $"http://localhost:{Port}/InterfaceDefaults/GetWithAttributes";
            DownloadStringAndAssertContains(address, "Done");
        }

        public void InterfaceDefaultsGetWithoutAttributes()
        {
            var address = $"http://localhost:{Port}/InterfaceDefaults/GetWithoutAttributes";
            DownloadStringAndAssertContains(address, "Done");
        }
    }
}
