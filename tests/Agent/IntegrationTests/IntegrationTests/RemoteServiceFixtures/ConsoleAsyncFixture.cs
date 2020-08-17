// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class ConsoleAsyncFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"ConsoleAsyncApplication";
        private const string ExecutableName = @"ConsoleAsyncApplication.exe";
        public ConsoleAsyncFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded))
        {
        }
    }
}
