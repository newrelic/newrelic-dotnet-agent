// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public class NetCoreAsyncTestsFixture : RemoteApplicationFixture
{
    private const string ApplicationDirectoryName = @"NetCoreAsyncApplication";
    private const string ExecutableName = @"NetCoreAsyncApplication.exe";
    public NetCoreAsyncTestsFixture() :
        base(new RemoteService(
            ApplicationDirectoryName,
            ExecutableName,
            Tfm.NetLatest,
            ApplicationType.Bounded,
            true,
            true,
            true))
    {
    }
}