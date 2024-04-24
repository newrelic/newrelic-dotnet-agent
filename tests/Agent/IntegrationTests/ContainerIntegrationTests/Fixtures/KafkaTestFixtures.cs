// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class KafkaTestFixtureBase : RemoteApplicationFixture
{
    protected override int MaxTries => 1;

    protected KafkaTestFixtureBase(
        string distroTag,
        ContainerApplication.Architecture containerArchitecture,
        string dockerfile,
        string dotnetVersion,
        string dockerComposeFile = "docker-compose-kafka.yml") :
        base(new ContainerApplication(distroTag, containerArchitecture, dotnetVersion, dockerfile, dockerComposeFile))
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

public class KafkaDotNet6TestFixture : KafkaTestFixtureBase
{
    private const string Dockerfile = "KafkaTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "bookworm-slim";
    private const string DotnetVersion = "6.0";

    public KafkaDotNet6TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}

public class KafkaDotNet8TestFixture : KafkaTestFixtureBase
{
    private const string Dockerfile = "KafkaTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "bookworm-slim";
    private const string DotnetVersion = "8.0";

    public KafkaDotNet8TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}
