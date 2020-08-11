// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AttributeInstrumentation : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = "AttributeInstrumentation";
        private const string ExecutableName = "AttributeInstrumentation.exe";
        private const string TargetFramework = "net461";

        public AttributeInstrumentation() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
        }
    }
}
