// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System.Net;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCore3BasicWebApiApplicationFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCore3BasicWebApiApplication";
        private const string ExecutableName = @"AspNetCore3BasicWebApiApplication.exe";
        private const string TargetFramework = "netcoreapp3.1";

        public AspNetCore3BasicWebApiApplicationFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, true, true))
        {
        }

        public string GetTraceId()
        {
            var address = $"http://localhost:{Port}/api/default/GetTraceId";
            var webClient = new WebClient();
            var result = webClient.DownloadString(address);
            Assert.NotNull(result);
            return result;
        }
    }
}
