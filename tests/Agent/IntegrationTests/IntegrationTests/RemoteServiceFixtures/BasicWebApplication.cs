/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicWebApplication : RemoteApplicationFixture
    {

        public string ResponseBody { get; private set; }

        public BasicWebApplication() : base(new RemoteWebApplication("BasicWebApplication", ApplicationType.Bounded))
        {
            Actions
            (
                exerciseApplication: Get
            );
        }

        public void Get()
        {
            // Two additional considerations being tested here:
            // 1. Metric is named as "DefAult.aspx".ToLower() (or "default.aspx") to keep casing clean (AspPagesTransactionNameWrapper.cs)
            // 2. Prevent a server redirect - Server strips the .aspx suffix and redirects to just "Default" before matching on "Default.aspx"
            var address = string.Format("http://{0}:{1}/DefAult", DestinationServerName, Port);
            DownloadStringAndAssertContains(address, "<html");
        }
    }
}
