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

public abstract class LinuxContainerTest<T> : NewRelicIntegrationTest<T> where T : ContainerTestFixtureBase
{
    const int ExpectedSentCount = 4;
    private readonly T _fixture;

    protected LinuxContainerTest(T fixture, ITestOutputHelper output) : base(fixture)
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

public class DebianX64ContainerTest : LinuxContainerTest<DebianX64ContainerTestFixture>
{
    public DebianX64ContainerTest(DebianX64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class UbuntuX64ContainerTest : LinuxContainerTest<UbuntuX64ContainerTestFixture>
{
    public UbuntuX64ContainerTest(UbuntuX64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
public class AlpineX64ContainerTest : LinuxContainerTest<AlpineX64ContainerTestFixture>
{
    public AlpineX64ContainerTest(AlpineX64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class DebianArm64ContainerTest : LinuxContainerTest<DebianArm64ContainerTestFixture>
{
    public DebianArm64ContainerTest(DebianArm64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class UbuntuArm64ContainerTest : LinuxContainerTest<UbuntuArm64ContainerTestFixture>
{
    public UbuntuArm64ContainerTest(UbuntuArm64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class CentosX64ContainerTest : LinuxContainerTest<CentosX64ContainerTestFixture>
{
    public CentosX64ContainerTest(CentosX64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class CentosArm64ContainerTest : LinuxContainerTest<CentosArm64ContainerTestFixture>
{
    public CentosArm64ContainerTest(CentosArm64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AmazonX64ContainerTest : LinuxContainerTest<AmazonX64ContainerTestFixture>
{
    public AmazonX64ContainerTest(AmazonX64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AmazonArm64ContainerTest : LinuxContainerTest<AmazonArm64ContainerTestFixture>
{
    public AmazonArm64ContainerTest(AmazonArm64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
public class FedoraX64ContainerTest : LinuxContainerTest<FedoraX64ContainerTestFixture>
{
    public FedoraX64ContainerTest(FedoraX64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class FedoraArm64ContainerTest : LinuxContainerTest<FedoraArm64ContainerTestFixture>
{
    public FedoraArm64ContainerTest(FedoraArm64ContainerTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
