// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNet5BasicWebApiApplicationFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNet5BasicWebApiApplication";
        private const string ExecutableName = @"AspNet5BasicWebApiApplication.exe";
        private const string TargetFramework = "net5.0";

        public AspNet5BasicWebApiApplicationFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, true, true))
        {
        }

        public void MakeExternalCallUsingHttpClient(string baseAddress, string path)
        {
            var address = $"http://localhost:{Port}/api/default/MakeExternalCallUsingHttpClient?baseAddress={baseAddress}&path={path}";
            using (var client = new HttpClient())
            {
                var response = client.GetStringAsync(address).Result;
                Assert.Contains("Worked", response);
            }
        }
    }
}
