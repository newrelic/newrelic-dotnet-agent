// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.ContainerFixtures;

public abstract class LinuxSmokeTestFixtureBase : ContainerFixture
{
    private static readonly string DotnetVersion = "7.0";

    protected LinuxSmokeTestFixtureBase(string applicationDirectoryName, string distroTag, ContainerApplication.Architecture containerArchitecture, string dockerfile) :
        base(new ContainerApplication(applicationDirectoryName, distroTag, containerArchitecture, DotnetVersion, dockerfile))
    {
    }

    public virtual void ExerciseApplication()
    {
        var address = $"http://localhost:{Port}/weatherforecast";
        GetAndAssertStatusCode(address, System.Net.HttpStatusCode.OK);
    }

    public void Delay(int seconds)
    {
        Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
    }
}

public class DebianX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "DebianX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "jammy";

    public DebianX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
}

public class UbuntuX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "UbuntuX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "bullseye-slim";

    public UbuntuX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
}
public class AlpineX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "AlpineX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "alpine";

    public AlpineX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
}

public class DebianArm64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "DebianArm64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private static readonly string DistroTag = "jammy";

    public DebianArm64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
}

public class UbuntuArm64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "UbuntuArm64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private static readonly string DistroTag = "bullseye-slim";

    public UbuntuArm64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
}

public class CentosX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "CentosX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "centos";

    public CentosX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
}
public class CentosArm64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "CentosArm64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private static readonly string DistroTag = "centos";

    public CentosArm64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
}

public class AmazonX64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "AmazonX64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "amazonlinux";

    public AmazonX64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
}
public class AmazonArm64SmokeTestFixture : LinuxSmokeTestFixtureBase
{
    private static readonly string Dockerfile = "SmokeTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "AmazonArm64SmokeTestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private static readonly string DistroTag = "amazonlinux";

    public AmazonArm64SmokeTestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile) { }
}
