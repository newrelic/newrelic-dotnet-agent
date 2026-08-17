// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public class AppDomainCachingEnabledContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "noble";

    public AppDomainCachingEnabledContainerTestFixture() : base(DistroTag, Architecture, Dockerfile)
    {
        ProfilerLogExpected = true;
    }
}

public class AppDomainCachingDisabledContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "noble";
    private const string DockerComposeFile = "docker-compose-appdomaincaching.yml";

    public AppDomainCachingDisabledContainerTestFixture() : base(DistroTag, Architecture, Dockerfile, DockerComposeFile)
    {
        ProfilerLogExpected = true;
        SetAdditionalEnvironmentVariable("NEW_RELIC_DISABLE_APPDOMAIN_CACHING", "true");
    }
}
