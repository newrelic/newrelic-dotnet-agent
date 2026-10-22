// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public class ConsoleInstrumentationLoaderFixture : RemoteApplicationFixture
{
    private const string ApplicationDirectoryName = @"ConsoleInstrumentationLoader";
    private const string ExecutableName = @"ConsoleInstrumentationLoader.exe";

    public ConsoleInstrumentationLoaderFixture()
        : base(new RemoteConsoleApplication(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded)
            .SetTimeout(TimeSpan.FromMinutes(2)))
    {
    }

}


public abstract class ConsoleInstrumentationLoaderFixtureCore : RemoteApplicationFixture
{
    private static readonly string ApplicationDirectoryName = @"ConsoleInstrumentationLoaderCore";
    private static readonly string ExecutableName = $"{ApplicationDirectoryName}.exe";

    protected ConsoleInstrumentationLoaderFixtureCore(string targetFramework)
        : base(new RemoteConsoleApplication(ApplicationDirectoryName, ExecutableName, targetFramework, ApplicationType.Bounded, true, true)
            .SetTimeout(TimeSpan.FromMinutes(2)))
    {

    }

}

public class ConsoleInstrumentationLoaderFixtureCoreOldest : ConsoleInstrumentationLoaderFixtureCore
{
    public ConsoleInstrumentationLoaderFixtureCoreOldest() : base(Tfm.NetOldest)
    {
    }
}

public class ConsoleInstrumentationLoaderFixtureCoreLatest : ConsoleInstrumentationLoaderFixtureCore
{
    public ConsoleInstrumentationLoaderFixtureCoreLatest() : base(Tfm.NetLatest)
    {
    }
}