// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCore3FeaturesFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCore3Features";
        private const string ExecutableName = @"AspNetCore3Features.exe";
        public AspNetCore3FeaturesFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, targetFramework: "net6.0", ApplicationType.Bounded, true, true, true))
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
            var webClient = new WebClient();
            Assert.Throws<System.Net.WebException>(() => webClient.DownloadString(address));
        }

        public void AccessCollectible()
        {
            var address = $"http://localhost:{Port}/Collectible/AccessCollectible";
            var webClient = new WebClient();
            webClient.DownloadString(address);
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
