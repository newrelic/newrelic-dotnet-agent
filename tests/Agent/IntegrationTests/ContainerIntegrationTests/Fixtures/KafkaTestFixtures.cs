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

        // Test custom statistics handlers (composite pattern) - integrated into main exercise
        GetAndAssertStatusCode(address + "producewithcustomstatistics", System.Net.HttpStatusCode.OK);
        var produceResult = GetString(address + "producewithcustomstatistics");
        Delay(2); // Allow time for async completion

        GetAndAssertStatusCode(address + "consumewithcustomstatistics", System.Net.HttpStatusCode.OK);
        var consumeResult = GetString(address + "consumewithcustomstatistics");
        Delay(2); // Allow time for async completion

        // Wait for statistics callbacks to trigger (they fire every 5 seconds)
        Delay(8); // Wait long enough for callbacks

        // Check status of customer handlers
        GetAndAssertStatusCode(address + "customstatisticsstatus", System.Net.HttpStatusCode.OK);
        var statusResult = GetString(address + "customstatisticsstatus");

        // Store results for later test validation
        CustomStatisticsResults = (produceResult, consumeResult, statusResult);

        Delay(3); // Final delay before shutdown
    }

    public (string produceResult, string consumeResult, string statusResult)? CustomStatisticsResults { get; private set; }

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
    private const string DistroTag = "noble";
    private const string DotnetVersion = "8.0";

    public KafkaDotNet8TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}

public class KafkaDotNet10TestFixture : KafkaTestFixtureBase
{
    private const string Dockerfile = "KafkaTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "noble";
    private const string DotnetVersion = "10.0";

    public KafkaDotNet10TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}
