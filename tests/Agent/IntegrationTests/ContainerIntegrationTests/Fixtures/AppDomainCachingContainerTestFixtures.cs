// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class AppDomainCachingEnabledContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = ContainerApplication.UbuntuDistroTag;

    protected AppDomainCachingEnabledContainerTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile)
    {
        ProfilerLogExpected = true;
    }
}

public class AppDomainCachingEnabledContainerTestFixtureCoreOldest : AppDomainCachingEnabledContainerTestFixture
{
    public AppDomainCachingEnabledContainerTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class AppDomainCachingEnabledContainerTestFixtureCoreLatest : AppDomainCachingEnabledContainerTestFixture
{
    public AppDomainCachingEnabledContainerTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}

public abstract class AppDomainCachingDisabledContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = ContainerApplication.UbuntuDistroTag;
    private const string DockerComposeFile = "docker-compose-appdomaincaching.yml";

    protected AppDomainCachingDisabledContainerTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile, DockerComposeFile)
    {
        ProfilerLogExpected = true;
        SetAdditionalEnvironmentVariable("NEW_RELIC_DISABLE_APPDOMAIN_CACHING", "true");
    }
}

public class AppDomainCachingDisabledContainerTestFixtureCoreOldest : AppDomainCachingDisabledContainerTestFixture
{
    public AppDomainCachingDisabledContainerTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class AppDomainCachingDisabledContainerTestFixtureCoreLatest : AppDomainCachingDisabledContainerTestFixture
{
    public AppDomainCachingDisabledContainerTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}
