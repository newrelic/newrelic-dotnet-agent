/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class ConsoleOtherTransactionWrapperFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"ConsoleOtherTransactionWrapperApplication";
        private const string ExecutableName = @"ConsoleOtherTransactionWrapperApplication.exe";
        public ConsoleOtherTransactionWrapperFixture() : base(new RemoteConsoleApplication(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, false, false))
        {
        }

    }
}
