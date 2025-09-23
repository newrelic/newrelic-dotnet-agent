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
        GetAndAssertStatusCode(address + "consumewithtimeout", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "consumewithtimeout", System.Net.HttpStatusCode.OK);

        GetAndAssertStatusCode(address + "produce", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "consumewithcancellationtoken", System.Net.HttpStatusCode.OK);

        // start a consume on an empty queue so we can verify that the Consume(CancellationToken) overload is correctly suppressing the Consume(int) overload calls
        GetAndAssertStatusCode(address + "consumewithcancellationtoken", System.Net.HttpStatusCode.OK);
        Delay(1); // wait a bit to ensure the consumer is started before we produce
        GetAndAssertStatusCode(address + "produceasync", System.Net.HttpStatusCode.OK); // produce after the consume is started so we know the consume will get a message
    }

    public string GetBootstrapServer()
    {
        var address = $"http://localhost:{Port}/kafka/bootstrap_server";
        var response = GetString(address);

        return response;
    }

    public void Delay(int seconds)
    {
        Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
    }
}

public class KafkaDotNet8TestFixture : KafkaTestFixtureBase
{
    private const string Dockerfile = "KafkaTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "bookworm-slim";
    private const string DotnetVersion = "8.0";

    public KafkaDotNet8TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}

public class KafkaDotNet9TestFixture : KafkaTestFixtureBase
{
    private const string Dockerfile = "KafkaTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "bookworm-slim";
    private const string DotnetVersion = "9.0";

    public KafkaDotNet9TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}
