// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public class RuntimeAsyncTestsFixture : RemoteApplicationFixture
{
    private const string ApplicationDirectoryName = @"RuntimeAsyncApplication";
    private const string ExecutableName = @"RuntimeAsyncApplication.exe";
    public RuntimeAsyncTestsFixture() :
        base(new RemoteService(
            ApplicationDirectoryName,
            ExecutableName,
            "net10.0",
            ApplicationType.Bounded,
            true,
            true,
            true))
    {
    }
}
