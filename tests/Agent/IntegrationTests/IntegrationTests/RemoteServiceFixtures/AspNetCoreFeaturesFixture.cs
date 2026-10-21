// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public abstract class AspNetCoreFeaturesFixture : RemoteApplicationFixture
{
    private const string ApplicationDirectoryName = @"AspNetCoreFeatures";
    private const string ExecutableName = @"AspNetCoreFeatures.exe";
    protected AspNetCoreFeaturesFixture(string targetFramework) :
        base(new RemoteService(
            ApplicationDirectoryName,
            ExecutableName,
            targetFramework: targetFramework,
            ApplicationType.Bounded,
            true,
            true,
            true))
    {
    }

    public void Get()
    {
        var address = $"http://{DestinationServerName}:{Port}/";
        GetStringAndAssertContains(address, "<html>");
    }

    public void ThrowException()
    {
        var address = $"http://{DestinationServerName}:{Port}/Home/ThrowException";
        GetAndAssertStatusCode(address, HttpStatusCode.InternalServerError);
    }

    public void AccessCollectible()
    {
        var address = $"http://{DestinationServerName}:{Port}/Collectible/AccessCollectible";
        _httpClient.GetStringAsync(address).Wait();
    }

    public void AsyncStream()
    {
        var address = $"http://{DestinationServerName}:{Port}/api/AsyncStream";
        GetStringAndAssertContains(address, "45");
    }

    public void InterfaceDefaultsGetWithAttributes()
    {
        var address = $"http://{DestinationServerName}:{Port}/InterfaceDefaults/GetWithAttributes";
        GetStringAndAssertContains(address, "Done");
    }

    public void InterfaceDefaultsGetWithoutAttributes()
    {
        var address = $"http://{DestinationServerName}:{Port}/InterfaceDefaults/GetWithoutAttributes";
        GetStringAndAssertContains(address, "Done");
    }
}

public class AspNetCoreFeaturesFixtureCoreOldest : AspNetCoreFeaturesFixture
{
    public AspNetCoreFeaturesFixtureCoreOldest() : base(Tfm.NetOldest)
    {
    }
}

public class AspNetCoreFeaturesFixtureCoreLatest : AspNetCoreFeaturesFixture
{
    public AspNetCoreFeaturesFixtureCoreLatest() : base(Tfm.NetLatest)
    {
    }
}
