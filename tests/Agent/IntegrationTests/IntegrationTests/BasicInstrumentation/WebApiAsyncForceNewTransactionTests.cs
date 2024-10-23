// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class WebApiAsyncForceNewTransactionTests_Instrumented : WebApiAsyncForceNewTransactionTests
    {
        private const decimal ExpectedWebTransactionCount = 6;
        private const decimal ExpectedOtherTransactionCount = 4;
        private const decimal ExpectedTransactionCount = ExpectedWebTransactionCount + ExpectedOtherTransactionCount;


        public WebApiAsyncForceNewTransactionTests_Instrumented(WebApiAsyncFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }

        protected override void SetupConfiguration(string instrumentationFilePath)
        {
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncFireAndForgetController", "AsyncMethod", "AsyncForceNewTransactionWrapper");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncFireAndForgetController", "SyncMethod", "AsyncForceNewTransactionWrapper");
        }


        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

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
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", CallCountAllHarvests = ExpectedTransactionCount},
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Apdex"},
            new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
            new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", CallCountAllHarvests = ExpectedWebTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransaction", CallCountAllHarvests = ExpectedWebTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", CallCountAllHarvests = ExpectedWebTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all", CallCountAllHarvests = ExpectedOtherTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime", CallCountAllHarvests = ExpectedOtherTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_AwaitedAsync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/AA-AA", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/AA-AM-AM", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_FireAndForget = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/AF-AF", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/AF-AM-AM", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_Sync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/AS-AS", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_AwaitedAsync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/SA-SA", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/SA-AM-AM", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_FireAndForget = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/SF-SF", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/FireAndForgetTests/SF-AM-AM", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_Sync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/SS-SS", callCount = 1 },
        };

    }

    [NetFrameworkTest]
    public class WebApiAsyncForceNewTransactionTests_NotInstrumented : WebApiAsyncForceNewTransactionTests
    {
        private const decimal ExpectedWebTransactionCount = 6;
        private const decimal ExpectedOtherTransactionCount = 0;
        private const decimal ExpectedTransactionCount = ExpectedWebTransactionCount + ExpectedOtherTransactionCount;

        public WebApiAsyncForceNewTransactionTests_NotInstrumented(WebApiAsyncFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }

        protected override void SetupConfiguration(string instrumentationFilePath)
        {
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncFireAndForgetController", "AsyncMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync");
            CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncFireAndForgetController", "SyncMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapper");
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", CallCountAllHarvests = ExpectedTransactionCount},
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Apdex"},
            new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
            new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", CallCountAllHarvests = ExpectedWebTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransaction", CallCountAllHarvests = ExpectedWebTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", CallCountAllHarvests = ExpectedWebTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_AwaitedAsync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/AA-AA", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Async_Sync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/AS-AS", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_AwaitedAsync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/SA-SA", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics_Sync_Sync = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/FireAndForgetTests/SS-SS", callCount = 1 },
        };


        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assert.NotNull(metrics);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(_generalMetrics, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Async_AwaitedAsync, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Async_Sync, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Sync_AwaitedAsync, metrics),
                () => Assertions.MetricsExist(_expectedMetrics_Sync_Sync, metrics),


                //Cannot determine async-fire and forget because it will probably throw an error, but can determine what it starts with
                () => Assert.Contains(metrics, x => x.MetricSpec.Name.StartsWith("WebTransaction/FireAndForgetTests/AF-", StringComparison.OrdinalIgnoreCase)),

                //Cannot determine sync-fire and forget because it will probably throw an error, but can determine what it starts with
                () => Assert.Contains(metrics, x => x.MetricSpec.Name.StartsWith("WebTransaction/FireAndForgetTests/SF-", StringComparison.OrdinalIgnoreCase)),

                //There shouldn't be any OtherTransactions because no instrumentation has been applied instructing
                //the agent to create another transaction.
                () => Assert.DoesNotContain(metrics, x => x.MetricSpec.Name.StartsWith("OtherTransaction", StringComparison.OrdinalIgnoreCase)),
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );
        }
    }

    public abstract class WebApiAsyncForceNewTransactionTests : NewRelicIntegrationTest<WebApiAsyncFixture>
    {
        protected readonly WebApiAsyncFixture _fixture;
        protected const string AssemblyName = "WebApiAsyncApplication";

        protected abstract void SetupConfiguration(string instrumentationFilePath);

        protected WebApiAsyncForceNewTransactionTests(WebApiAsyncFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);

                    var instrumentationFilePath = Path.Combine(_fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");

                    SetupConfiguration(instrumentationFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.GetAsync_AwaitedAsync();
                    _fixture.GetAsync_FireAndForget();
                    _fixture.GetAsync_Sync();

                    _fixture.GetSync_AwaitedAsync();
                    _fixture.GetSync_FireAndForget();
                    _fixture.GetSync_Sync();

                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }
    }

}
