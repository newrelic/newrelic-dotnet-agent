// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class ContainerTestFixtureBase : RemoteApplicationFixture
{
    private const string DotnetVersion = "7.0";

    protected override int MaxTries => 1;

    protected ContainerTestFixtureBase(string applicationDirectoryName, string distroTag, ContainerApplication.Architecture containerArchitecture, string dockerfile) :
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

public class DebianX64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const string DockerComposeServiceName = "DebianX64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "jammy";

    public DebianX64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}

public class UbuntuX64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const string DockerComposeServiceName = "UbuntuX64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "bullseye-slim";

    public UbuntuX64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}
public class AlpineX64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const string DockerComposeServiceName = "AlpineX64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "alpine";

    public AlpineX64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}

public class DebianArm64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const string DockerComposeServiceName = "DebianArm64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private const string DistroTag = "jammy";

    public DebianArm64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}

public class UbuntuArm64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile";
    private const string DockerComposeServiceName = "UbuntuArm64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private const string DistroTag = "bullseye-slim";

    public UbuntuArm64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}

public class CentosX64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.centos";
    private const string DockerComposeServiceName = "CentosX64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "centos";

    public CentosX64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}
public class CentosArm64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.centos";
    private const string DockerComposeServiceName = "CentosArm64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private const string DistroTag = "centos";

    public CentosArm64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}

public class AmazonX64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.amazon";
    private const string DockerComposeServiceName = "AmazonX64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "amazonlinux";

    public AmazonX64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}
public class AmazonArm64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.amazon";
    private const string DockerComposeServiceName = "AmazonArm64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private const string DistroTag = "amazonlinux";

    public AmazonArm64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}

public class FedoraX64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.fedora";
    private const string DockerComposeServiceName = "FedoraX64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "fedora";

    public FedoraX64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}
public class FedoraArm64ContainerTestFixture : ContainerTestFixtureBase
{
    private const string Dockerfile = "SmokeTestApp/Dockerfile.fedora";
    private const string DockerComposeServiceName = "FedoraArm64ContainerTestApp";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.Arm64;
    private const string DistroTag = "fedora";

    public FedoraArm64ContainerTestFixture() : base(DockerComposeServiceName, DistroTag, Architecture, Dockerfile) { }
}
