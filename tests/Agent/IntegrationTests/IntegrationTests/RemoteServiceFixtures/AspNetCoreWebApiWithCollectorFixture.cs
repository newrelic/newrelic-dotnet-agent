// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public abstract class AspNetCoreWebApiWithCollectorFixture : MockNewRelicFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCoreBasicWebApiApplication";
        private const string ExecutableName = @"AspNetCoreBasicWebApiApplication.exe";

        protected AspNetCoreWebApiWithCollectorFixture(string TargetFramework) : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, true, true))
        {
        }


        public void Get()
        {
            var address = $"http://localhost:{Port}/api/default/AwesomeName";
            DownloadStringAndAssertContains(address, "Chuck Norris");
        }
    }

    public class AspNetCoreWebApiWithCollectorFixture_net50 : AspNetCoreWebApiWithCollectorFixture
    {
        public AspNetCoreWebApiWithCollectorFixture_net50() : base("net5.0")
        {
        }
    }

    public class AspNetCoreWebApiWithCollectorFixture_net60 : AspNetCoreWebApiWithCollectorFixture
    {
        public AspNetCoreWebApiWithCollectorFixture_net60() : base("net6.0")
        {
        }
    }


}
