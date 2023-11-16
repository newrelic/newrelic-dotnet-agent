// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreMvcCoreFrameworkFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = "AspNetCoreMvcCoreFrameworkApplication";
        private const string ExecutableName = "AspNetCoreMvcCoreFrameworkApplication.exe";
        private const string TargetFramework = "net462";

        public AspNetCoreMvcCoreFrameworkFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, false, true))
        {
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/values";
            GetStringAndAssertContains(address, "value1");
        }
    }
}
