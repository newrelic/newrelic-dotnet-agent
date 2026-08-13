// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

/// <summary>
/// Fixture base for the continuous-profiling Linux container tests. Builds the SmokeTestApp with the
/// continuous-profiling Dockerfile (CP enabled via NEW_RELIC_CONTINUOUS_PROFILING_* env) and exercises the
/// CPU-burn endpoint synchronously on the request (traced) thread so the native Linux sampler captures a
/// thread with an active trace/span. Each drain POSTs the built profile to the configured OTLP endpoint,
/// but the test validates the native sampler purely from the agent log (log-based only).
/// </summary>
public abstract class ContinuousProfilingContainerTestFixtureBase : ContainerTestFixtureBase
{
    // Seconds of synchronous, on-CPU work inside the instrumented web transaction. Long enough to span
    // several 1000 ms sampling intervals so a sample is reliably taken while the transaction is active.
    private const int BurnSeconds = 8;

    protected ContinuousProfilingContainerTestFixtureBase(string distroTag, ContainerApplication.Architecture containerArchitecture, string dockerfile)
        : base(distroTag, containerArchitecture, dockerfile)
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

public class ContinuousProfilingUbuntuX64ContainerTestFixture : ContinuousProfilingContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.continuousprofiling";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "noble"; // Ubuntu 24.04

    public ContinuousProfilingUbuntuX64ContainerTestFixture() : base(DistroTag, Architecture, Dockerfile) { }
}

public class ContinuousProfilingUbuntuArm64ContainerTestFixture : ContinuousProfilingContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.continuousprofiling";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private const string DistroTag = "noble"; // Ubuntu 24.04

    public ContinuousProfilingUbuntuArm64ContainerTestFixture() : base(DistroTag, Architecture, Dockerfile) { }
}
