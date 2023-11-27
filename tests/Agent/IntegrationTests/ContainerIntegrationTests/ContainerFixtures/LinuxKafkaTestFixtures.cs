// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;

namespace NewRelic.Agent.ContainerIntegrationTests.ContainerFixtures;

public abstract class LinuxKafkaTestFixtureBase : ContainerFixture
{
    protected LinuxKafkaTestFixtureBase(
        string applicationDirectoryName,
        string distroTag,
        ContainerApplication.Architecture containerArchitecture,
        string dockerfile,
        string dotnetVersion) :
        base(new ContainerApplication(applicationDirectoryName, distroTag, containerArchitecture, dotnetVersion, dockerfile))
    {
    }

    public virtual void ExerciseApplication()
    {
        var address = $"http://localhost:{Port}/kafka/";
        GetAndAssertStatusCode(address + "produce", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "produceasync", System.Net.HttpStatusCode.OK);
    }

    public void Delay(int seconds)
    {
        Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
    }
}

public class UbuntuX64Kafka1TestFixture : LinuxKafkaTestFixtureBase
{
    private static readonly string Dockerfile = "KafkaTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "UbuntuX64Kafka1TestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "bullseye-slim";
    private static readonly string DotnetVersion = "6.0";

    public UbuntuX64Kafka1TestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}

public class UbuntuX64Kafka2TestFixture : LinuxKafkaTestFixtureBase
{
    private static readonly string Dockerfile = "KafkaTestApp/Dockerfile";
    private static readonly string ApplicationDirectoryName = "UbuntuX64Kafka2TestApp";
    private static readonly ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private static readonly string DistroTag = "bullseye-slim";
    private static readonly string DotnetVersion = "7.0";

    public UbuntuX64Kafka2TestFixture() : base(ApplicationDirectoryName, DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}
