// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public abstract class RuntimeAsyncTestsFixture : RemoteApplicationFixture
{
    private const string ApplicationDirectoryName = @"RuntimeAsyncApplication";
    private const string ExecutableName = @"RuntimeAsyncApplication.exe";

    protected RuntimeAsyncTestsFixture(string targetFramework) :
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

public class RuntimeAsyncTestsFixtureCoreOldest : RuntimeAsyncTestsFixture
{
    public RuntimeAsyncTestsFixtureCoreOldest() : base(Tfm.NetOldest)
    {
    }
}

public class RuntimeAsyncTestsFixtureCoreLatest : RuntimeAsyncTestsFixture
{
    public RuntimeAsyncTestsFixtureCoreLatest() : base(Tfm.NetLatest)
    {
    }
}
