// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreWebApiCustomAttributesFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = "AspNetCoreWebApiCustomAttributesApplication";
        private const string ExecutableName = "AspNetCoreWebApiCustomAttributesApplication.exe";

        public AspNetCoreWebApiCustomAttributesFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, "net7.0", ApplicationType.Bounded, true, true, true))
        {
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/CustomAttributes";
            GetStringAndAssertEqual(address, "success");
        }
    }
    public class HSMAspNetCoreWebApiCustomAttributesFixture : AspNetCoreWebApiCustomAttributesFixture
    {
        public override string TestSettingCategory { get { return "HSM"; } }
    }

}
