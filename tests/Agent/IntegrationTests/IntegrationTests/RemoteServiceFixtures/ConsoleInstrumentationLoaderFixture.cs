// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.IO;
using System.Reflection;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
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


    public class ConsoleInstrumentationLoaderFixtureCore : RemoteApplicationFixture
    {
        private static readonly string ApplicationDirectoryName = @"ConsoleInstrumentationLoaderCore";
        private static readonly string ExecutableName = $"{ApplicationDirectoryName}.exe";

        public ConsoleInstrumentationLoaderFixtureCore()
            : base(new RemoteConsoleApplication(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, true, true)
                  .SetTimeout(TimeSpan.FromMinutes(2)))
        {

        }

    }
}
