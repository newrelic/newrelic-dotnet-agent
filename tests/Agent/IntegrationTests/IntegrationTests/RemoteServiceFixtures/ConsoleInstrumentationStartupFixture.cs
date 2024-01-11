// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.IO;
using System.Reflection;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class ConsoleInstrumentationStartupFixtureCore : RemoteApplicationFixture
    {
        private static readonly string ApplicationDirectoryName = @"ConsoleInstrumentationStartup";
        private static readonly string ExecutableName = $"{ApplicationDirectoryName}.exe";

        public ConsoleInstrumentationStartupFixtureCore()
            : base(new RemoteConsoleApplication(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, true, true)
                  .SetTimeout(TimeSpan.FromMinutes(2)))
        {

        }

    }
}
