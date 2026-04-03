// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

public abstract class MassTransitTestBase<T> : NewRelicIntegrationTest<T> where T : MassTransitTestFixtureBase
{
    private readonly string _kafkaTopicName;
    private readonly string _rabbitMqQueueName;
    private readonly T _fixture;

    protected MassTransitTestBase(T fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _kafkaTopicName = GenerateRandomName();
        _rabbitMqQueueName = "mt-test-" + GenerateRandomName().ToLowerInvariant();

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("debug");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);

                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("MASSTRANSIT_KAFKA_TOPIC", _kafkaTopicName);
                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("MASSTRANSIT_RABBITMQ_QUEUE", _rabbitMqQueueName);
            },
            exerciseApplication: () =>
            {
                _fixture.Delay(20); // wait for kafka, rabbitmq, and app to be ready
                _fixture.TestLogger.WriteLine("Starting exercise application");
                _fixture.ExerciseApplication();

                _fixture.TestLogger.WriteLine("Waiting for metrics to be harvested");
                _fixture.Delay(30); // wait for metric harvest after exercising
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromSeconds(11));

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        // === Kafka Rider ===
        // Produce goes through Confluent.Kafka wrapper; consume goes through MassTransit filter.
        // Before the fix (#3519), consume showed Queue/Named/Unknown.
        var kafkaConsume = $"MessageBroker/MassTransit/Topic/Consume/Named/{_kafkaTopicName}";
        var kafkaConsumeTransaction = $"OtherTransaction/Message/MassTransit/Topic/Named/{_kafkaTopicName}";
        var kafkaProduce = $"MessageBroker/Kafka/Topic/Produce/Named/{_kafkaTopicName}";

        // === RabbitMQ ===
        // Publish and send go through the MassTransit filter (produce side).
        // Consume goes through the MassTransit filter (consume side).
        var rabbitMqProduceRegex = @"MessageBroker\/MassTransit\/Queue\/Produce\/Named\/([^\/]+)";
        var rabbitMqConsumeRegex = @"MessageBroker\/MassTransit\/Queue\/Consume\/Named\/([^\/]+)";

        // InMemory (MultiBus) metrics also flow through the MassTransit filter.
        // Queue names are auto-generated so we don't assert on specific names,
        // but the "no Unknown" check below validates InMemory parsing works.

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            // --- Kafka ---
            new() { metricName = kafkaConsume, CallCountAllHarvests = 2 },
            new() { metricName = kafkaConsume, metricScope = kafkaConsumeTransaction, CallCountAllHarvests = 2 },
            new() { metricName = kafkaProduce, CallCountAllHarvests = 2 },

            // --- RabbitMQ + InMemory (both produce Queue metrics) ---
            // RabbitMQ: 2 produce (publish + send), 2 consume. InMemory: 1 produce, 1 consume.
            new() { metricName = rabbitMqConsumeRegex, IsRegexName = true, CallCountAllHarvests = 3 },
            new() { metricName = rabbitMqProduceRegex, IsRegexName = true, CallCountAllHarvests = 3 },

            // --- Distributed tracing across all transports ---
            new() { metricName = "Supportability/TraceContext/Create/Success" },
            new() { metricName = "Supportability/TraceContext/Accept/Success" },
        };

        // Verify no "Unknown" queue names appear in any MassTransit metrics
        var unknownMetrics = metrics
            .Where(m => m.MetricSpec.Name.Contains("MassTransit") && m.MetricSpec.Name.Contains("Unknown"))
            .ToList();

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assert.Empty(unknownMetrics)
        );
    }

    internal static string GenerateRandomName()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < 15; i++)
        {
            var shifter = RandomNumberGenerator.GetInt32(0, 26);
            builder.Append(Convert.ToChar(shifter + 65));
        }
        return builder.ToString();
    }
}

[Collection("MassTransitTests")]
[Trait("Architecture", "amd64")]
[Trait("Distro", "Ubuntu")]
public class MassTransitDotNet8Test : MassTransitTestBase<MassTransitDotNet8TestFixture>
{
    public MassTransitDotNet8Test(MassTransitDotNet8TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[Collection("MassTransitTests")]
[Trait("Architecture", "amd64")]
[Trait("Distro", "Ubuntu")]
public class MassTransitDotNet10Test : MassTransitTestBase<MassTransitDotNet10TestFixture>
{
    public MassTransitDotNet10Test(MassTransitDotNet10TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
