// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class MemcachedTestFixtureBase : RemoteApplicationFixture
{
    protected override int MaxTries => 1;
    public readonly string DotnetVer;

    protected MemcachedTestFixtureBase(
        string distroTag,
        ContainerApplication.Architecture containerArchitecture,
        string dockerfile,
        string dotnetVersion,
        string dockerComposeFile = "docker-compose-memcached.yml") :
        base(new ContainerApplication(distroTag, containerArchitecture, dotnetVersion, dockerfile, dockerComposeFile, "MemcachedTestApp"))
    {
        DotnetVer = dotnetVersion;
    }

    public virtual void ExerciseApplication()
    {
        var address = $"http://localhost:{Port}/memcached/";
        GetAndAssertStatusCode(address + "testallmethods", System.Net.HttpStatusCode.OK);
    }

    public void Delay(int seconds)
    {
        Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
    }
}

public class MemcachedDotNet6TestFixture : MemcachedTestFixtureBase
{
    private const string Dockerfile = "MemcachedTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "bookworm-slim";
    private const string DotnetVersion = "6.0";

    public MemcachedDotNet6TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}

public class MemcachedDotNet8TestFixture : MemcachedTestFixtureBase
{
    private const string Dockerfile = "MemcachedTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "bookworm-slim";
    private const string DotnetVersion = "8.0";

    public MemcachedDotNet8TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}
