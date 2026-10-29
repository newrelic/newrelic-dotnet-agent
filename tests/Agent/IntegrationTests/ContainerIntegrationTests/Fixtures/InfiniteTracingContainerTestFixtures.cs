// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class InfiniteTracingAlpineX64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "alpine";

    protected InfiniteTracingAlpineX64ContainerTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile) { }
}

public class InfiniteTracingAlpineX64ContainerTestFixtureCoreOldest : InfiniteTracingAlpineX64ContainerTestFixture
{
    public InfiniteTracingAlpineX64ContainerTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class InfiniteTracingAlpineX64ContainerTestFixtureCoreLatest : InfiniteTracingAlpineX64ContainerTestFixture
{
    public InfiniteTracingAlpineX64ContainerTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}

public abstract class InfiniteTracingAmazonX64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.amazon";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "amazonlinux";

    protected InfiniteTracingAmazonX64ContainerTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile) { }
}

public class InfiniteTracingAmazonX64ContainerTestFixtureCoreOldest : InfiniteTracingAmazonX64ContainerTestFixture
{
    public InfiniteTracingAmazonX64ContainerTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class InfiniteTracingAmazonX64ContainerTestFixtureCoreLatest : InfiniteTracingAmazonX64ContainerTestFixture
{
    public InfiniteTracingAmazonX64ContainerTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}

public abstract class InfiniteTracingAmazonArm64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.amazon";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private const string DistroTag = "amazonlinux";

    protected InfiniteTracingAmazonArm64ContainerTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile) { }
}

public class InfiniteTracingAmazonArm64ContainerTestFixtureCoreOldest : InfiniteTracingAmazonArm64ContainerTestFixture
{
    public InfiniteTracingAmazonArm64ContainerTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class InfiniteTracingAmazonArm64ContainerTestFixtureCoreLatest : InfiniteTracingAmazonArm64ContainerTestFixture
{
    public InfiniteTracingAmazonArm64ContainerTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}
