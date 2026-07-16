// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

public abstract class AppDomainCachingContainerTest<T> : NewRelicIntegrationTest<T> where T : ContainerTestFixtureBase
{
    private readonly T _fixture;
    private readonly string _expectedStrategy;
    private readonly bool _cachingDisabled;

    protected AppDomainCachingContainerTest(T fixture, ITestOutputHelper output, string expectedStrategy, bool cachingDisabled) : base(fixture)
    {
        _fixture = fixture;
        _expectedStrategy = expectedStrategy;
        _cachingDisabled = cachingDisabled;
        _fixture.TestLogger = output;

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
            },
            exerciseApplication: () =>
            {
                _fixture.ExerciseApplication();

                // On loaded CI runners container startup can exceed 10s, causing the first
                // harvest to fire before the HTTP request completes. Wait for the transaction
                // to be confirmed processed, then wait for a second metric harvest so we are
                // guaranteed at least one harvest that includes the AppDomainCaching supportability metric.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1), 2);

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void ProfilerLogsExpectedCallingStrategy()
    {
        Assert.Contains($"Calls to the managed agent will use the calling strategy - {_expectedStrategy}", _fixture.ProfilerLog.GetFullLogAsString());
    }

    [Fact]
    public void SupportabilityMetricReported()
    {
        var actualMetrics = _fixture.AgentLog.GetMetrics();
        if (_cachingDisabled)
        {
            Assert.Contains(actualMetrics, x => x.MetricSpec.Name == "Supportability/DotNET/AppDomainCaching/Disabled");
        }
        else
        {
            Assert.DoesNotContain(actualMetrics, x => x.MetricSpec.Name == "Supportability/DotNET/AppDomainCaching/Disabled");
        }
    }
}

[Trait("Architecture", "amd64")]
[Trait("Distro", "Ubuntu")]
public class AppDomainCachingEnabledContainerTest : AppDomainCachingContainerTest<AppDomainCachingEnabledContainerTestFixture>
{
    public AppDomainCachingEnabledContainerTest(AppDomainCachingEnabledContainerTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output, "AppDomain Fallback Cache", cachingDisabled: false)
    {
    }
}

[Trait("Architecture", "amd64")]
[Trait("Distro", "Ubuntu")]
public class AppDomainCachingDisabledContainerTest : AppDomainCachingContainerTest<AppDomainCachingDisabledContainerTestFixture>
{
    public AppDomainCachingDisabledContainerTest(AppDomainCachingDisabledContainerTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output, "Reflection", cachingDisabled: true)
    {
    }
}
