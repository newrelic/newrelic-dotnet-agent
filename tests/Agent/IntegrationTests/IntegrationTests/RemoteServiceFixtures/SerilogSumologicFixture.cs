// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class SerilogSumologicFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"SerilogSumologicApplication";
        private const string ExecutableName = @"SerilogSumologicApplication.exe";
        public SerilogSumologicFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, "net7.0", ApplicationType.Bounded, true, true, true))
        {
        }

        public void SyncControllerMethod()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/SyncControllerMethod";
            GetStringAndAssertContains(address, "<html>");
        }

        public void AsyncControllerMethod()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/AsyncControllerMethod";
            GetStringAndAssertContains(address, "<html>");
        }
    }
}
