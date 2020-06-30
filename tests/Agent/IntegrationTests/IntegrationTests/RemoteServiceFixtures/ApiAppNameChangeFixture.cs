/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class ApiAppNameChangeFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"ApiAppNameChange";
        private const string ExecutableName = @"NewRelic.Agent.IntegrationTests.Applications.ApiAppNameChange.exe";
        private const string TargetFramework = "net451";

        public ApiAppNameChangeFixture()
            : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
        }
    }
}
