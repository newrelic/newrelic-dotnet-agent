// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AppDomainCaching;

public abstract class AppDomainCachingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private bool _appDomainCachingDisabled;
    private readonly string _expectedCallingStrategy;

    public AppDomainCachingTestsBase(TFixture fixture, ITestOutputHelper output, bool appDomainCachingDisabled, string expectedCallingStrategy) : base(fixture)
    {
        _fixture = fixture;
        _appDomainCachingDisabled = appDomainCachingDisabled;
        _expectedCallingStrategy = expectedCallingStrategy;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        _fixture.AddCommand($"RootCommands InstrumentedMethodToStartAgent");

        if(_appDomainCachingDisabled)
        {
            _fixture.SetAdditionalEnvironmentVariable("NEW_RELIC_DISABLE_APPDOMAIN_CACHING", _appDomainCachingDisabled ? "true" : "false");
        }

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                configModifier.DisableEventListenerSamplers(); // Required for .NET 8 to pass.
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForConnect(TimeSpan.FromSeconds(30));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void ProfilerObservesEnvironmentVariable()
    {
        // The profiler logs the resolved managed-agent calling strategy at startup.
        // NOTE: Today the profiler forces the Reflection strategy on CoreCLR regardless of
        // NEW_RELIC_DISABLE_APPDOMAIN_CACHING, because ModuleLoadFinished does not inject the
        // AppDomain-cache helper stubs into System.Private.CoreLib. That is why the .NET (Core)
        // "enabled" variant below expects "Reflection" rather than "AppDomain Fallback Cache".
        // FUTURE: when Core gains AppDomain-fallback support (NR-184027 Milestone B), update the
        // expected strategy for the Core "enabled" case to "AppDomain Fallback Cache".
        Assert.Contains($"Calls to the managed agent will use the calling strategy - {_expectedCallingStrategy}", _fixture.ProfilerLog.GetFullLogAsString());
    }

    [Fact]
    public void SupportabilityMetricReported()
    {
        var actualMetrics = _fixture.AgentLog.GetMetrics();
        if (_appDomainCachingDisabled)
        {
            Assert.Contains(actualMetrics, x => x.MetricSpec.Name == "Supportability/DotNET/AppDomainCaching/Disabled");
        }
        else
        {
            Assert.DoesNotContain(actualMetrics, x => x.MetricSpec.Name == "Supportability/DotNET/AppDomainCaching/Disabled");
        }
    }
}

#region Enabled (not disabled) tests
public class AppDomainCachingEnabledTestsFWLatestTests : AppDomainCachingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public AppDomainCachingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, "AppDomain Fallback Cache")
    {
    }
}

public class AppDomainCachingEnabledTestsNetCoreLatestTests : AppDomainCachingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public AppDomainCachingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, "Reflection")
    {
    }
}
#endregion

#region Disabled tests
public class AppDomainCachingDisabledTestsFWLatestTests : AppDomainCachingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public AppDomainCachingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, "Reflection")
    {
    }
}

public class AppDomainCachingDisabledTestsNetCoreLatestTests : AppDomainCachingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public AppDomainCachingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, "Reflection")
    {
    }
}
#endregion