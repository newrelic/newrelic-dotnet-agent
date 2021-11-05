// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests
{
    public class OwinTracingChainFixture : TracingChainFixture
    {
        private const string ApplicationDirectoryName = @"Owin4WebApi";
        private const string ExecutableName = @"Owin4WebApi.exe";
        private const string TargetFramework = "net462";

        public OwinTracingChainFixture() :
            this(ApplicationDirectoryName, ExecutableName, TargetFramework)
        {
        }
        protected OwinTracingChainFixture(string ApplicationDirectoryName, string ExecutableName, string TargetFramework) : base(ApplicationDirectoryName, ExecutableName, TargetFramework)
        {
        }
    }
}
