// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System.Net.Http;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public abstract class AspNetCoreBasicWebApiApplicationFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCoreBasicWebApiApplication";
        private const string ExecutableName = @"AspNetCoreBasicWebApiApplication.exe";

        protected AspNetCoreBasicWebApiApplicationFixture(string TargetFramework) : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, true, true))
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
    public class AspNetCoreBasicWebApiApplicationFixture_net50 : AspNetCoreBasicWebApiApplicationFixture
    {
        public AspNetCoreBasicWebApiApplicationFixture_net50() : base("net5.0")
        {
        }
    }

    public class AspNetCoreBasicWebApiApplicationFixture_net60 : AspNetCoreBasicWebApiApplicationFixture
    {
        public AspNetCoreBasicWebApiApplicationFixture_net60() : base("net6.0")
        {
        }
    }

}
