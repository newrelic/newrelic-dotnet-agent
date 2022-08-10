// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class SerilogSumologicFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"SerilogSumologicApplication";
        private const string ExecutableName = @"SerilogSumologicApplication.exe";
        public SerilogSumologicFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, "netcoreapp3.1", ApplicationType.Bounded, true, true, true))
        {
        }

        public void SyncControllerMethod()
        {
            var address = $"http://127.0.0.1:{Port}/Home/SyncControllerMethod";
            DownloadStringAndAssertContains(address, "<html>");
        }

        public void AsyncControllerMethod()
        {
            var address = $"http://127.0.0.1:{Port}/Home/AsyncControllerMethod";
            DownloadStringAndAssertContains(address, "<html>");
        }
    }
}
