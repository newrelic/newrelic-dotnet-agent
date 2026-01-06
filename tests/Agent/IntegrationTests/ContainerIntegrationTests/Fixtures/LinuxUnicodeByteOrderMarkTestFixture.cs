// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures
{
    public class LinuxUnicodeByteOrderMarkTestFixture : ContainerTestFixtureBase
    {
        private const string Dockerfile = "SmokeTestApp/Dockerfile";
        private const string DockerComposeServiceName = "LinuxUnicodeByteOrderMarkTestFixture";
        private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
        private const string DistroTag = "noble";

        public LinuxUnicodeByteOrderMarkTestFixture() : base(DistroTag, Architecture, Dockerfile) { }
    }
}
