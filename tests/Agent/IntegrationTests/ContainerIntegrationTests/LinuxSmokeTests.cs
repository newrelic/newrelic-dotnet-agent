// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.ContainerIntegrationTests.ContainerFixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace ContainerIntegrationTests;

public abstract class LinuxSmokeTest<T> : NewRelicIntegrationTest<T> where T : LinuxSmokeTestFixtureBase
{
    private readonly T _fixture;

    protected LinuxSmokeTest(T fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterMetricsHarvestCycle(5);
                configModifier.LogToConsole();
            },
            exerciseApplication: () =>
            {
                _fixture.ExerciseApplication();

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromSeconds(10));

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

public class DebianX64SmokeTest : LinuxSmokeTest<DebianX64SmokeTestFixture>
{
    public DebianX64SmokeTest(DebianX64SmokeTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class UbuntuX64SmokeTest : LinuxSmokeTest<UbuntuX64SmokeTestFixture>
{
    public UbuntuX64SmokeTest(UbuntuX64SmokeTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
public class AlpineX64SmokeTest : LinuxSmokeTest<AlpineX64SmokeTestFixture>
{
    public AlpineX64SmokeTest(AlpineX64SmokeTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class DebianArm64SmokeTest : LinuxSmokeTest<DebianArm64SmokeTestFixture>
{
    public DebianArm64SmokeTest(DebianArm64SmokeTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class UbuntuArm64SmokeTest : LinuxSmokeTest<UbuntuArm64SmokeTestFixture>
{
    public UbuntuArm64SmokeTest(UbuntuArm64SmokeTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class CentosX64SmokeTest : LinuxSmokeTest<CentosX64SmokeTestFixture>
{
    public CentosX64SmokeTest(CentosX64SmokeTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class CentosArm64SmokeTest : LinuxSmokeTest<CentosArm64SmokeTestFixture>
{
    public CentosArm64SmokeTest(CentosArm64SmokeTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AmazonX64SmokeTest : LinuxSmokeTest<AmazonX64SmokeTestFixture>
{
    public AmazonX64SmokeTest(AmazonX64SmokeTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AmazonArm64SmokeTest : LinuxSmokeTest<AmazonArm64SmokeTestFixture>
{
    public AmazonArm64SmokeTest(AmazonArm64SmokeTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
