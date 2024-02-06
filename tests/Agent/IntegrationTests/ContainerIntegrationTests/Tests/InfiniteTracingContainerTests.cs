// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

public abstract class InfiniteTracingContainerTest<T> : NewRelicIntegrationTest<T> where T : ContainerTestFixtureBase
{
    const int ExpectedSentCount = 4;
    private readonly T _fixture;

    protected InfiniteTracingContainerTest(T fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);

                // enable 8T to verify protobuf is working correctly across linux distros
                configModifier.ForceTransactionTraces()
                    .EnableDistributedTrace()
                    .EnableInfiniteTracing(_fixture.TestConfiguration.TraceObserverUrl, _fixture.TestConfiguration.TraceObserverPort);

                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                configModifier.SetLogLevel("Finest");
                configModifier.LogToConsole();
            },
            exerciseApplication: () =>
            {
                // Wait for 8T to connect
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanStreamingServiceStreamConnectedLogLineRegex, TimeSpan.FromSeconds(15));

                _fixture.ExerciseApplication();

                _fixture.Delay(12); // wait long enough to ensure a metric harvest occurs after we exercise the app
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromSeconds(11));

                // Now wait to see that the 8T spans were sent successfully
                _fixture.AgentLog.WaitForLogLinesCapturedIntCount(AgentLogBase.SpanStreamingSuccessfullySentLogLineRegex, TimeSpan.FromMinutes(1), ExpectedSentCount);

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var actualMetrics = _fixture.AgentLog.GetMetrics().ToList();

        Assert.Contains(actualMetrics, m => m.MetricSpec.Name.Equals("WebTransaction/MVC/WeatherForecast/Get"));

        // verify 8T metrics
        var expectedSeenCount = 4;

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Seen", callCount = expectedSeenCount },
            new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Sent", callCount = ExpectedSentCount },
        };

        Assertions.MetricsExist(expectedMetrics, actualMetrics);
    }
}

// only testing on a subset of linux distros to keep total test runtime under control. Additional distros can be uncommented below if needed.

public class DebianX64InfiniteTracingContainerTest(DebianX64ContainerTestFixture fixture, ITestOutputHelper output)
    : InfiniteTracingContainerTest<DebianX64ContainerTestFixture>(fixture, output);

//public class UbuntuX64InfiniteTracingContainerTest(UbuntuX64ContainerTestFixture fixture, ITestOutputHelper output)
//    : InfiniteTracingContainerTest<UbuntuX64ContainerTestFixture>(fixture, output);

public class AlpineX64InfiniteTracingContainerTest(AlpineX64ContainerTestFixture fixture, ITestOutputHelper output)
    : InfiniteTracingContainerTest<AlpineX64ContainerTestFixture>(fixture, output);

public class DebianArm64InfiniteTracingContainerTest(DebianArm64ContainerTestFixture fixture, ITestOutputHelper output)
    : InfiniteTracingContainerTest<DebianArm64ContainerTestFixture>(fixture, output);

//public class UbuntuArm64InfiniteTracingContainerTest(UbuntuArm64ContainerTestFixture fixture, ITestOutputHelper output)
//    : InfiniteTracingContainerTest<UbuntuArm64ContainerTestFixture>(fixture, output);

//public class CentosX64InfiniteTracingContainerTest(CentosX64ContainerTestFixture fixture, ITestOutputHelper output)
//    : InfiniteTracingContainerTest<CentosX64ContainerTestFixture>(fixture, output);

//public class CentosArm64InfiniteTracingContainerTest(CentosArm64ContainerTestFixture fixture, ITestOutputHelper output)
//    : InfiniteTracingContainerTest<CentosArm64ContainerTestFixture>(fixture, output);

public class AmazonX64InfiniteTracingContainerTest(AmazonX64ContainerTestFixture fixture, ITestOutputHelper output)
    : InfiniteTracingContainerTest<AmazonX64ContainerTestFixture>(fixture, output);

public class AmazonArm64InfiniteTracingContainerTest(AmazonArm64ContainerTestFixture fixture, ITestOutputHelper output)
    : InfiniteTracingContainerTest<AmazonArm64ContainerTestFixture>(fixture, output);
//public class FedoraX64InfiniteTracingContainerTest(FedoraX64ContainerTestFixture fixture, ITestOutputHelper output)
//    : InfiniteTracingContainerTest<FedoraX64ContainerTestFixture>(fixture, output);

//public class FedoraArm64InfiniteTracingContainerTest(FedoraArm64ContainerTestFixture fixture, ITestOutputHelper output)
//    : InfiniteTracingContainerTest<FedoraArm64ContainerTestFixture>(fixture, output);
