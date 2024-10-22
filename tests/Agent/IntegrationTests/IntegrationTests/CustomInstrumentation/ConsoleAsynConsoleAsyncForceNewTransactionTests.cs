// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetFrameworkTest]
    public class ConsoleAsyncForceNewTransactionTests_Instrumented : ConsoleAsyncForceNewTransactionTests
    {
        private const decimal ExpectedTransactionCount = 10;

        public ConsoleAsyncForceNewTransactionTests_Instrumented(ConsoleAsyncFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }

        protected override void SetupConfiguration(string instrumentationFilePath)
        {
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "SyncMethod", "AsyncForceNewTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "AsyncMethod", "AsyncForceNewTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Async_AwaitedAsync", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Async_FireAndForget", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Async_Sync", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Sync_AwaitedAsync", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Sync_FireAndForget", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Sync_Sync", "OtherTransactionWrapper");
        }

        [Fact]
        public void Test()
        {
            var metrics = Fixture.AgentLog.GetMetrics().ToList();

            var metricNames = metrics.Select(x => x.MetricSpec.Name).OrderBy(x => x).ToList();

            Assert.NotNull(metrics);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(_generalMetrics, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Async_AwaitedAsync, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Async_FireAndForget, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Async_Sync, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Sync_AwaitedAsync, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Sync_FireAndForget, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Sync_Sync, metrics),
                () => Assert.Empty(Fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(Fixture.AgentLog.GetErrorEvents())
            );
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", CallCountAllHarvests = ExpectedTransactionCount},
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime", CallCountAllHarvests = ExpectedTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_AwaitedAsync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/AA-AA", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/AA-AM-AM", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_FireAndForget = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/AF-AF", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/AF-AM-AM", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_Sync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/AS-AS", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_AwaitedAsync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/SA-SA", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/SA-AM-AM", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_FireAndForget = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/SF-SF", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/SF-AM-AM", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_Sync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/SS-SS", callCount = 1 },
        };

    }

    [NetFrameworkTest]
    public class ConsoleAsyncForceNewTransactionTests_NotInstrumented : ConsoleAsyncForceNewTransactionTests
    {
        private const decimal ExpectedTransactionCount = 6;

        public ConsoleAsyncForceNewTransactionTests_NotInstrumented(ConsoleAsyncFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }

        protected override void SetupConfiguration(string instrumentationFilePath)
        {
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "SyncMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "AsyncMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync");

            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Async_AwaitedAsync", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Async_FireAndForget", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Async_Sync", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Sync_AwaitedAsync", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Sync_FireAndForget", "OtherTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.AsyncFireAndForgetUseCases", "Sync_Sync", "OtherTransactionWrapper");
        }

        [Fact]
        public void Test()
        {
            var metrics = Fixture.AgentLog.GetMetrics().ToList();

            var metricNames = metrics.Select(x => x.MetricSpec.Name).OrderBy(x => x).ToList();

            Assert.NotNull(metrics);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(_generalMetrics, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Async_AwaitedAsync, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Async_Sync, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Sync_AwaitedAsync, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Sync_Sync, metrics),
                () => Assert.Empty(Fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(Fixture.AgentLog.GetErrorEvents())
            );
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", CallCountAllHarvests = ExpectedTransactionCount},
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime", CallCountAllHarvests = ExpectedTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_AwaitedAsync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/AA-AA", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_Sync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/AS-AS", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_AwaitedAsync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/SA-SA", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_Sync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/SS-SS", callCount = 1 },
        };

    }

    public abstract class ConsoleAsyncForceNewTransactionTests : NewRelicIntegrationTest<ConsoleAsyncFixture>
    {
        protected readonly ConsoleAsyncFixture Fixture;
        protected const string AssemblyName = "ConsoleAsyncApplication";

        protected abstract void SetupConfiguration(string instrumentationFilePath);

        public ConsoleAsyncForceNewTransactionTests(ConsoleAsyncFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;
            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = Fixture.DestinationNewRelicConfigFilePath;

                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    Fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                    Fixture.RemoteApplication.NewRelicConfig.ConfigureFasterMetricsHarvestCycle(30);
                    Fixture.RemoteApplication.NewRelicConfig.ConfigureFasterSpanEventsHarvestCycle(30);

                    SetupConfiguration(instrumentationFilePath);

                },
                exerciseApplication: () =>
                {
                    Fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );
            Fixture.Initialize();
        }
    }
}
