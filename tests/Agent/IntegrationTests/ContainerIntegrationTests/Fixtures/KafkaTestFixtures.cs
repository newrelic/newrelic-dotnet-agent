// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class KafkaTestFixtureBase : RemoteApplicationFixture
{
    private const int TopicNameLength = 15;

    protected override int MaxTries => 1;

    // Generated once per fixture lifetime. Test classes in [Collection("KafkaTests")] reuse the
    // same fixture instance, but each [Fact] method re-constructs the test class — so any
    // per-test state (topic name, bootstrap server) must live on the fixture to stay consistent
    // across the Initialize-once/exercise-once fixture lifecycle.
    public string TopicName { get; } = GenerateTopic();

    // Captured during ExerciseApplication. Null until the first (and only) Initialize has run.
    public string BootstrapServer { get; private set; }

    protected KafkaTestFixtureBase(
        string distroTag,
        ContainerApplication.Architecture containerArchitecture,
        string dockerfile,
        string dotnetVersion,
        string dockerComposeFile = "docker-compose-kafka.yml") :
        base(new ContainerApplication(distroTag, containerArchitecture, dotnetVersion, dockerfile, dockerComposeFile))
    {
    }

    private static string GenerateTopic()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < TopicNameLength; i++)
        {
            var shifter = RandomNumberGenerator.GetInt32(0, 26);
            builder.Append(Convert.ToChar(shifter + 65));
        }

        return builder.ToString();
    }

    public virtual void ExerciseApplication()
    {
        var address = $"http://localhost:{Port}/kafka/";

        GetAndAssertStatusCode(address + "produce", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "produceasync", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "consumewithtimeout", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "consumewithtimeout", System.Net.HttpStatusCode.OK);

        // produce with pre-existing DT headers to verify agent replaces them
        GetAndAssertStatusCode(address + "produceasyncwithexistingheaders", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "consumewithtimeout", System.Net.HttpStatusCode.OK);

        GetAndAssertStatusCode(address + "produce", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "consumewithcancellationtoken", System.Net.HttpStatusCode.OK);

        // start a consume on an empty queue so we can verify that the Consume(CancellationToken) overload is correctly suppressing the Consume(int) overload calls
        GetAndAssertStatusCode(address + "consumewithcancellationtoken", System.Net.HttpStatusCode.OK);
        Delay(1); // wait a bit to ensure the consumer is started before we produce
        GetAndAssertStatusCode(address + "produceasync", System.Net.HttpStatusCode.OK); // produce after the consume is started so we know the consume will get a message

        // Exercise the producer-side composite handler path — one extra produce on the
        // long-lived custom-stats producer. The custom-stats consumer is always running
        // in the background of the test app, so it needs no explicit trigger here.
        GetAndAssertStatusCode(address + "producewithcustomstatistics", System.Net.HttpStatusCode.OK);
        GetAndAssertStatusCode(address + "producewithcustomstatistics", System.Net.HttpStatusCode.OK);

        // Status read happens at the end of the exercise. Both long-lived custom-stats
        // clients have been alive since container startup and will have fired multiple
        // librdkafka statistics callbacks by now (statistics.interval.ms = 5000).
        CustomStatisticsStatus = GetString(address + "customstatisticsstatus");
    }

    public string CustomStatisticsStatus { get; private set; }

    public string GetBootstrapServer()
    {
        var address = $"http://localhost:{Port}/kafka/bootstrap_server";
        var response = GetString(address);

        BootstrapServer = response;
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
