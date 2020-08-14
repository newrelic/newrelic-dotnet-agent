// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicOpenRastaApplication : RemoteApplicationFixture
    {
        public string ResponseBody { get; private set; }

        public BasicOpenRastaApplication() : base(new RemoteWebApplication("OpenRastaWebApplication", ApplicationType.Bounded))
        {
        }

        public void GetWithQueryString()
        {
            var address = $"http://{DestinationServerName}:{Port}/home?key=value";
            DownloadStringAndAssertContains(address, "GET");
        }


    }
}
