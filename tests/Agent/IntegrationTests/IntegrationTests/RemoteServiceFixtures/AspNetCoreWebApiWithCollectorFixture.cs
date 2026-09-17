// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public class AspNetCoreWebApiWithCollectorFixture : MockNewRelicFixture
{
    public AspNetCoreWebApiWithCollectorFixture() :
        base(new RemoteService(
            "AspNetCoreBasicWebApiApplication",
            "AspNetCoreBasicWebApiApplication.exe",
            "net10.0",
            ApplicationType.Bounded,
            true,
            true,
            true))
    {
    }


    public void Get()
    {
        var address = $"http://{DestinationServerName}:{Port}/api/default/AwesomeName";
        GetStringAndAssertContains(address, "Chuck Norris");
    }

    public void BurnCpu(int seconds = 8)
    {
        var address = $"http://{DestinationServerName}:{Port}/api/default/BurnCpu?seconds={seconds}";
        GetStringAndAssertContains(address, "burned cpu");
    }
}