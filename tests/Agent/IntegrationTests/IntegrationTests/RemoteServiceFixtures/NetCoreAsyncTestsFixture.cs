// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public abstract class NetCoreAsyncTestsFixture : RemoteApplicationFixture
{
    private const string ApplicationDirectoryName = @"NetCoreAsyncApplication";
    private const string ExecutableName = @"NetCoreAsyncApplication.exe";
    protected NetCoreAsyncTestsFixture(string targetFramework) :
        base(new RemoteService(
            ApplicationDirectoryName,
            ExecutableName,
            targetFramework,
            ApplicationType.Bounded,
            true,
            true,
            true))
    {
    }
}

public class NetCoreAsyncTestsFixtureCoreOldest : NetCoreAsyncTestsFixture
{
    public NetCoreAsyncTestsFixtureCoreOldest() : base(Tfm.NetOldest)
    {
    }
}

public class NetCoreAsyncTestsFixtureCoreLatest : NetCoreAsyncTestsFixture
{
    public NetCoreAsyncTestsFixtureCoreLatest() : base(Tfm.NetLatest)
    {
    }
}
