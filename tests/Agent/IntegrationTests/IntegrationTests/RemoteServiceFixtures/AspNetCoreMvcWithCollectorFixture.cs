// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreMvcWithCollectorFixture : MockNewRelicFixture
    {
        public AspNetCoreMvcWithCollectorFixture() :
            base(new RemoteService("AspNetCoreMvcBasicRequestsApplication",
                "AspNetCoreMvcBasicRequestsApplication.exe",
                "net6.0",
                ApplicationType.Bounded,
                true,
                true,
                true))
        {
        }

        public void Get()
        {
            var address = $"http://localhost:{Port}/";
            DownloadStringAndAssertContains(address, "<html>");
        }
    }
}
