// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System.Net.Http;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public abstract class AspNetCoreBasicWebApiApplicationFixture : RemoteApplicationFixture
    {
        protected AspNetCoreBasicWebApiApplicationFixture(string ApplicationDirectoryName, string ExecutableName, string TargetFramework) : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, true, true))
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
        public AspNetCoreBasicWebApiApplicationFixture_net50() : base("AspNetCore5BasicWebApiApplication", "AspNetCore5BasicWebApiApplication.exe", "net5.0")
        {
        }
    }

    public class AspNetCoreBasicWebApiApplicationFixture_net60 : AspNetCoreBasicWebApiApplicationFixture
    {
        public AspNetCoreBasicWebApiApplicationFixture_net60() : base("AspNetCore6BasicWebApiApplication", "AspNetCore6BasicWebApiApplication.exe", "net6.0")
        {
        }
    }

    public class AspNetCoreBasicWebApiApplicationFixture_net70 : AspNetCoreBasicWebApiApplicationFixture
    {
        public AspNetCoreBasicWebApiApplicationFixture_net70() : base("AspNetCore7BasicWebApiApplication", "AspNetCore7BasicWebApiApplication.exe", "net7.0")
        {
        }
    }
}
