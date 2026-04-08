// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class MassTransitTestFixtureBase : RemoteApplicationFixture
{
    protected override int MaxTries => 1;

    protected MassTransitTestFixtureBase(
        string distroTag,
        ContainerApplication.Architecture containerArchitecture,
        string dockerfile,
        string dotnetVersion,
        string dockerComposeFile = "docker-compose-masstransit.yml") :
        base(new ContainerApplication(distroTag, containerArchitecture, dotnetVersion, dockerfile, dockerComposeFile))
    {
    }

    public virtual void ExerciseApplication()
    {
        var address = $"http://localhost:{Port}/";

        // Kafka: produce two messages (consumed automatically by Rider)
        GetAndAssertStatusCode(address + "kafka/produce", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "kafka/produce", System.Net.HttpStatusCode.OK);

        // RabbitMQ: publish and send (consumed by the configured receive endpoint)
        GetAndAssertStatusCode(address + "rabbitmq/publish", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "rabbitmq/send", System.Net.HttpStatusCode.OK);

        // InMemory: publish via MultiBus (consumed by auto-configured endpoint)
        GetAndAssertStatusCode(address + "inmemory/publish", System.Net.HttpStatusCode.OK);
    }

    public void Delay(int seconds)
    {
        Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
    }
}

public class MassTransitDotNet8TestFixture : MassTransitTestFixtureBase
{
    private const string Dockerfile = "MassTransitTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "noble";
    private const string DotnetVersion = "8.0";

    public MassTransitDotNet8TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}

public class MassTransitDotNet10TestFixture : MassTransitTestFixtureBase
{
    private const string Dockerfile = "MassTransitTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "noble";
    private const string DotnetVersion = "10.0";

    public MassTransitDotNet10TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}
