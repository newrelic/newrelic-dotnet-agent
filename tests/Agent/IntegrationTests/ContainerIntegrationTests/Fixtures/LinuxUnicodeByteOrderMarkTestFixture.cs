// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class LinuxUnicodeByteOrderMarkTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const string DockerComposeServiceName = "LinuxUnicodeByteOrderMarkTestFixture";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "resolute";

    protected LinuxUnicodeByteOrderMarkTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile) { }
}

public class LinuxUnicodeByteOrderMarkTestFixtureCoreOldest : LinuxUnicodeByteOrderMarkTestFixture
{
    public LinuxUnicodeByteOrderMarkTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class LinuxUnicodeByteOrderMarkTestFixtureCoreLatest : LinuxUnicodeByteOrderMarkTestFixture
{
    public LinuxUnicodeByteOrderMarkTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}