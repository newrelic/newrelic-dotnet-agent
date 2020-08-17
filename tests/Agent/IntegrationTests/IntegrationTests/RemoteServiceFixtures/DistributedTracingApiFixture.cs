// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class DistributedTracingApiFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"DistributedTracingApiApplication";
        private const string ExecutableName = @"DistributedTracingApiApplication.exe";
        private const string TargetFramework = "net461";

        public DistributedTracingApiFixture()
            : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
        }
    }
}
