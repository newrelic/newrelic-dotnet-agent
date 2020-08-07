// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class NetCoreAttributeInstrumentationFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"NetCoreAttributeInstrumentationApplication";
        private const string ExecutableName = @"NetCoreAttributeInstrumentationApplication.exe";
        public NetCoreAttributeInstrumentationFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, true, true))
        {
        }
    }
}
