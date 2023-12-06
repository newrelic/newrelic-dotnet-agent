// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

public abstract class LinuxContainerTest<T> : NewRelicIntegrationTest<T> where T : ContainerTestFixtureBase
{
    private readonly T _fixture;

    protected LinuxContainerTest(T fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.LogToConsole();
            },
            exerciseApplication: () =>
            {
                _fixture.ExerciseApplication();

                _fixture.Delay(11); // wait long enough to ensure a metric harvest occurs after we exercise the app
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromSeconds(11));

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var actualMetrics = _fixture.AgentLog.GetMetrics();

        Assert.Contains(actualMetrics, m => m.MetricSpec.Name.Equals("WebTransaction/MVC/WeatherForecast/Get"));
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
