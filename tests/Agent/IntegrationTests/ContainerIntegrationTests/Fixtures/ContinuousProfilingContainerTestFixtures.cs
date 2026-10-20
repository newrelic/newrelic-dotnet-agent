// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

/// <summary>
/// Fixture base for the continuous-profiling Linux container tests (CP enabled via
/// NEW_RELIC_PROFILING_* env in the Dockerfile). See ContinuousProfilingContainerTest for why
/// the CPU-burn endpoint and log-based-only assertions are used.
/// </summary>
public abstract class ContinuousProfilingContainerTestFixtureBase : ContainerTestFixtureBase
{
    // Seconds of synchronous, on-CPU work inside the instrumented web transaction. Long enough to span
    // several 1000 ms sampling intervals so a sample is reliably taken while the transaction is active.
    private const int BurnSeconds = 8;

    protected ContinuousProfilingContainerTestFixtureBase(string distroTag, ContainerApplication.Architecture containerArchitecture, string dotnetVersion, string dockerfile)
        : base(distroTag, containerArchitecture, dotnetVersion, dockerfile)
    {
    }

    public override void ExerciseApplication()
    {
        // Blocks for ~BurnSeconds while the endpoint burns CPU inside the WebTransaction; that on-CPU
        // window is what makes a captured sample's trace/span link reliably observable.
        var address = $"http://localhost:{Port}/continuousprofiling/burncpu?seconds={BurnSeconds}";
        GetAndAssertStatusCode(address, System.Net.HttpStatusCode.OK);
    }
}

public abstract class ContinuousProfilingUbuntuX64ContainerTestFixture : ContinuousProfilingContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.continuousprofiling";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "noble"; // Ubuntu 24.04

    protected ContinuousProfilingUbuntuX64ContainerTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile) { }
}

public class ContinuousProfilingUbuntuX64ContainerTestFixtureCoreOldest : ContinuousProfilingUbuntuX64ContainerTestFixture
{
    public ContinuousProfilingUbuntuX64ContainerTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class ContinuousProfilingUbuntuX64ContainerTestFixtureCoreLatest : ContinuousProfilingUbuntuX64ContainerTestFixture
{
    public ContinuousProfilingUbuntuX64ContainerTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}

public abstract class ContinuousProfilingUbuntuArm64ContainerTestFixture : ContinuousProfilingContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.continuousprofiling";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private const string DistroTag = "noble"; // Ubuntu 24.04

    protected ContinuousProfilingUbuntuArm64ContainerTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile) { }
}

public class ContinuousProfilingUbuntuArm64ContainerTestFixtureCoreOldest : ContinuousProfilingUbuntuArm64ContainerTestFixture
{
    public ContinuousProfilingUbuntuArm64ContainerTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class ContinuousProfilingUbuntuArm64ContainerTestFixtureCoreLatest : ContinuousProfilingUbuntuArm64ContainerTestFixture
{
    public ContinuousProfilingUbuntuArm64ContainerTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}

public abstract class ContinuousProfilingAlpineX64ContainerTestFixture : ContinuousProfilingContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.continuousprofiling";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "alpine";

    protected ContinuousProfilingAlpineX64ContainerTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile) { }
}

public class ContinuousProfilingAlpineX64ContainerTestFixtureCoreOldest : ContinuousProfilingAlpineX64ContainerTestFixture
{
    public ContinuousProfilingAlpineX64ContainerTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class ContinuousProfilingAlpineX64ContainerTestFixtureCoreLatest : ContinuousProfilingAlpineX64ContainerTestFixture
{
    public ContinuousProfilingAlpineX64ContainerTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}
