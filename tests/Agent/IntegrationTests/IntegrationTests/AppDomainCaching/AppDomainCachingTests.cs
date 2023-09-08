// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AppDomainCaching
{
    public abstract class AppDomainCachingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private bool _appDomainCachingDisabled;

        public AppDomainCachingTestsBase(TFixture fixture, ITestOutputHelper output, bool appDomainCachingDisabled) : base(fixture)
        {
            _fixture = fixture;
            _appDomainCachingDisabled = appDomainCachingDisabled;
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
            if( _appDomainCachingDisabled)
            {
                Assert.Contains("The use of AppDomain for method information caching is disabled", _fixture.ProfilerLog.GetFullLogAsString());
            }
            else
            {
                Assert.DoesNotContain("The use of AppDomain for method information caching is disabled", _fixture.ProfilerLog.GetFullLogAsString());
            }
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
    [NetFrameworkTest]
    public class AppDomainCachingEnabledTestsFWLatestTests : AppDomainCachingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AppDomainCachingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class AppDomainCachingEnabledTestsNetCoreLatestTests : AppDomainCachingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public AppDomainCachingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }
    #endregion

    #region Disabled tests
    [NetFrameworkTest]
    public class AppDomainCachingDisabledTestsFWLatestTests : AppDomainCachingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AppDomainCachingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class AppDomainCachingDisabledTestsNetCoreLatestTests : AppDomainCachingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public AppDomainCachingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }
    #endregion

}
