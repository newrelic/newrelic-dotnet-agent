﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.ContainerFixtures;

public abstract class LinuxSmokeTestFixtureBase : ContainerFixture
{
    protected LinuxSmokeTestFixtureBase(string applicationDirectoryName, string distroTag, ContainerApplication.Architecture containerArchitecture) :
        base(new ContainerApplication(applicationDirectoryName, distroTag, containerArchitecture))
    {
    }

    public virtual void ExerciseApplication()
    {
        var address = $"http://localhost:{Port}/weatherforecast";
        GetAndAssertStatusCode(address, System.Net.HttpStatusCode.OK);
    }
}

public class DebianX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string ApplicationDirectoryName = "DebianX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "7.0-jammy";

    public DebianX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture) { }
}

public class UbuntuX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string ApplicationDirectoryName = "UbuntuX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "7.0-bullseye-slim";

    public UbuntuX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture) { }
}
public class AlpineX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string ApplicationDirectoryName = "AlpineX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "7.0-alpine";

    public AlpineX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture) { }
}

public class DebianArm64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string ApplicationDirectoryName = "DebianArm64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private static readonly string DistroTag = "7.0-jammy";

    public DebianArm64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture) { }
}

public class UbuntuArm64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string ApplicationDirectoryName = "UbuntuArm64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private static readonly string DistroTag = "7.0-bullseye-slim";

    public UbuntuArm64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture) { }
}

public class CentosX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string ApplicationDirectoryName = "CentosX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "Centos";

    public CentosX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture) { }
}
public class CentosArm64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string ApplicationDirectoryName = "CentosArm64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private static readonly string DistroTag = "Centos";

    public CentosArm64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture) { }
}

public class AmazonX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string ApplicationDirectoryName = "AmazonX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "Amazon";

    public AmazonX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture) { }
}
public class AmazonArm64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string ApplicationDirectoryName = "AmazonArm64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private static readonly string DistroTag = "Amazon";

    public AmazonArm64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture) { }
}