// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class LinuxUnicodeLogFileTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const string DockerComposeServiceName = "LinuxUnicodeLogfileTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "resolute";

    protected LinuxUnicodeLogFileTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile)
    {
        ProfilerLogExpected = true;
    }
}

public class LinuxUnicodeLogFileTestFixtureCoreOldest : LinuxUnicodeLogFileTestFixture
{
    public LinuxUnicodeLogFileTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class LinuxUnicodeLogFileTestFixtureCoreLatest : LinuxUnicodeLogFileTestFixture
{
    public LinuxUnicodeLogFileTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}