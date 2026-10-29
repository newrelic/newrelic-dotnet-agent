// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public abstract class ConsoleInstrumentationStartupFixtureCore : RemoteApplicationFixture
{
    private static readonly string ApplicationDirectoryName = @"ConsoleInstrumentationStartup";
    private static readonly string ExecutableName = $"{ApplicationDirectoryName}.exe";

    protected ConsoleInstrumentationStartupFixtureCore(string targetFramework)
        : base(new RemoteConsoleApplication(ApplicationDirectoryName, ExecutableName, targetFramework, ApplicationType.Bounded, true, true)
            .SetTimeout(TimeSpan.FromMinutes(2)))
    {

    }

}

public class ConsoleInstrumentationStartupFixtureCoreOldest : ConsoleInstrumentationStartupFixtureCore
{
    public ConsoleInstrumentationStartupFixtureCoreOldest() : base(Tfm.NetOldest)
    {
    }
}

public class ConsoleInstrumentationStartupFixtureCoreLatest : ConsoleInstrumentationStartupFixtureCore
{
    public ConsoleInstrumentationStartupFixtureCoreLatest() : base(Tfm.NetLatest)
    {
    }
}