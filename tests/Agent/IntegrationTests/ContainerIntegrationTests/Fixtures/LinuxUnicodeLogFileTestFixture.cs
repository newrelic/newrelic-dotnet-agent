// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures
{
    public class LinuxUnicodeLogFileTestFixture : ContainerTestFixtureBase
    {
        private const string Dockerfile = "SmokeTestApp/Dockerfile";
        private const string DockerComposeServiceName = "LinuxUnicodeLogfileTestApp";
        private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
        private const string DistroTag = "jammy";

        public LinuxUnicodeLogFileTestFixture() : base(DistroTag, Architecture, Dockerfile) { }
    }
}
