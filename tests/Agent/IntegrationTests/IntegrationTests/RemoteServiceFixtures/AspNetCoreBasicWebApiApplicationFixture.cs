// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public abstract class AspNetCoreBasicWebApiApplicationFixture : RemoteApplicationFixture
{
    protected AspNetCoreBasicWebApiApplicationFixture(string targetFramework) :
        base(new RemoteService("AspNetCoreBasicWebApiApplication",
            "AspNetCoreBasicWebApiApplication.exe",
            targetFramework,
            ApplicationType.Bounded,
            true,
            true,
            true))
    {
    }

    public void MakeExternalCallUsingHttpClient(string baseAddress, string path)
    {
        var address = $"http://{DestinationServerName}:{Port}/api/default/MakeExternalCallUsingHttpClient?baseAddress={baseAddress}&path={path}";
        GetStringAndAssertContains(address, "Worked");
    }

    public string GetTraceId()
    {
        var address = $"http://{DestinationServerName}:{Port}/api/default/GetTraceId";
        return GetStringAndAssertIsNotNull(address);
    }
}

public class AspNetCoreBasicWebApiApplicationFixtureCoreOldest : AspNetCoreBasicWebApiApplicationFixture
{
    public AspNetCoreBasicWebApiApplicationFixtureCoreOldest() : base(Tfm.NetOldest)
    {
    }
}

public class AspNetCoreBasicWebApiApplicationFixtureCoreLatest : AspNetCoreBasicWebApiApplicationFixture
{
    public AspNetCoreBasicWebApiApplicationFixtureCoreLatest() : base(Tfm.NetLatest)
    {
    }
}