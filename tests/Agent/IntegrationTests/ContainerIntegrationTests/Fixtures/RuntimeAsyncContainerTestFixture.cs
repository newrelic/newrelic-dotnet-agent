// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

/// <summary>
/// Runs the runtime-async use cases on Linux. Extends RemoteApplicationFixture rather than
/// ContainerTestFixtureBase because that base hardcodes the default compose file and service name.
///
/// DOTNET_RuntimeAsync=1 is baked into this app's Dockerfile rather than set here:
/// SetAdditionalEnvironmentVariable lands a variable in the `docker compose up` host process only,
/// and compose forwards it into the container solely when a compose file names it via ${VAR}.
/// </summary>
public abstract class RuntimeAsyncContainerTestFixture : RemoteApplicationFixture
{
    private const string Dockerfile = "RuntimeAsyncTestApp/Dockerfile";
    private const string ComposeFile = "docker-compose-runtimeasync.yml";
    private const string ServiceName = "runtimeasynctestapp";
    private const string DistroTag = ContainerApplication.UbuntuDistroTag; // glibc, which is what the homefolders artifact ships
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;

    protected override int MaxTries => 1;

    protected RuntimeAsyncContainerTestFixture(string dotnetVersion)
        : base(new ContainerApplication(DistroTag, Architecture, dotnetVersion, Dockerfile, ComposeFile, ServiceName))
    {
    }

    /// <summary>
    /// Only a liveness check. The telemetry under test is recorded at application startup, before
    /// the web host comes up, because the use cases' [Transaction] attributes have to create
    /// OtherTransactions -- invoked from a request handler they would become segments of that
    /// request's WebTransaction instead.
    /// </summary>
    public virtual void ExerciseApplication()
    {
        var address = $"http://localhost:{Port}/status";
        GetAndAssertStatusCode(address, System.Net.HttpStatusCode.OK);
    }

    public void Delay(int seconds)
    {
        Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
    }
}

public class RuntimeAsyncContainerTestFixtureCoreOldest : RuntimeAsyncContainerTestFixture
{
    public RuntimeAsyncContainerTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class RuntimeAsyncContainerTestFixtureCoreLatest : RuntimeAsyncContainerTestFixture
{
    public RuntimeAsyncContainerTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}
