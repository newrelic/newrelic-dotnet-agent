// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicOpenRastaApplication : RemoteApplicationFixture
    {

        public string ResponseBody { get; private set; }

        public BasicOpenRastaApplication() : base(new RemoteWebApplication("OpenRastaWebApplication", ApplicationType.Bounded))
        {
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/home";
            var result = new WebClient().DownloadString(address);

            Assert.NotNull(result);
            Assert.Contains("GET", result);
        }


    }
}
