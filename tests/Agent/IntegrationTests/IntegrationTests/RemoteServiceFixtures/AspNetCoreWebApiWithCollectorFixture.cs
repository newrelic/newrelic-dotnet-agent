// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public abstract class AspNetCoreWebApiWithCollectorFixture : MockNewRelicFixture
    {
        protected AspNetCoreWebApiWithCollectorFixture(string ApplicationDirectoryName, string ExecutableName, string TargetFramework) : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, true, true))
        {
        }


        public void Get()
        {
            var address = $"http://127.0.0.1:{Port}/api/default/AwesomeName";
            DownloadStringAndAssertContains(address, "Chuck Norris");
        }
    }

    public class AspNetCoreWebApiWithCollectorFixture_net50 : AspNetCoreWebApiWithCollectorFixture
    {
        public AspNetCoreWebApiWithCollectorFixture_net50() : base("AspNetCore5BasicWebApiApplication", "AspNetCore5BasicWebApiApplication.exe", "net5.0")
        {
        }
    }

    public class AspNetCoreWebApiWithCollectorFixture_net60 : AspNetCoreWebApiWithCollectorFixture
    {
        public AspNetCoreWebApiWithCollectorFixture_net60() : base("AspNetCore6BasicWebApiApplication", "AspNetCore6BasicWebApiApplication.exe", "net6.0")
        {
        }
    }

}
