// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.ContainerIntegrationTests.ContainerFixtures
{
    public class LinuxUnicodeLogFileTestFixture : LinuxSmokeTestFixtureBase
    {
        private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
        private static readonly string ApplicationDirectoryName = "LinuxUnicodeLogfileTestApp";
        private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
        private static readonly string DistroTag = "jammy";

        public LinuxUnicodeLogFileTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
    }
}
