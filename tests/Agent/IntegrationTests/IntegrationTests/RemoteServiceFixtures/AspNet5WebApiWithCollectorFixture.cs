// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNet5WebApiWithCollectorFixture : MockNewRelicFixture
    {
        public AspNet5WebApiWithCollectorFixture() : base(new RemoteService("AspNet5BasicWebApiApplication", "AspNet5BasicWebApiApplication.exe", "net6.0", ApplicationType.Bounded, true, true, true))
        {
        }

        public void Get()
        {
            var address = $"http://localhost:{Port}/api/default/AwesomeName";
            DownloadStringAndAssertContains(address, "Chuck Norris");
        }
    }
}
