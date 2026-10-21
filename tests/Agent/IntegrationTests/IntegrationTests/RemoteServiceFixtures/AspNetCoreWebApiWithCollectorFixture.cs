// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public abstract class AspNetCoreWebApiWithCollectorFixture : MockNewRelicFixture
{
    protected AspNetCoreWebApiWithCollectorFixture(string targetFramework) :
        base(new RemoteService(
            "AspNetCoreBasicWebApiApplication",
            "AspNetCoreBasicWebApiApplication.exe",
            targetFramework,
            ApplicationType.Bounded,
            true,
            true,
            true), targetFramework)
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

public class AspNetCoreWebApiWithCollectorFixtureCoreOldest : AspNetCoreWebApiWithCollectorFixture
{
    public AspNetCoreWebApiWithCollectorFixtureCoreOldest() : base(Tfm.NetOldest)
    {
    }
}

public class AspNetCoreWebApiWithCollectorFixtureCoreLatest : AspNetCoreWebApiWithCollectorFixture
{
    public AspNetCoreWebApiWithCollectorFixtureCoreLatest() : base(Tfm.NetLatest)
    {
    }
}