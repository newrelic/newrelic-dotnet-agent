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
    public class WebApiAsyncTests : NewRelicIntegrationTest<WebApiAsyncFixture>
    {
        private readonly WebApiAsyncFixture _fixture;
        private const int ExpectedTransactionCount = 6;
        private const string AssemblyName = "WebApiAsyncApplication";

        public WebApiAsyncTests(WebApiAsyncFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);

                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitController", "CustomMethodAsync1", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync1");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitController", "CustomMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync2");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitController", "CustomMethodAsync3", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync3");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitController", "ConfigureAwaitFalseExampleAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitFalseExampleAsync");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitController", "ConfigureAwaitSubMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitSubMethodAsync2");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitController", "TaskRunBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskRunBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitController", "TaskFactoryStartNewBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskFactoryStartNewBackgroundMethod");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.ManualAsyncController", "TaskRunBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ManualTaskRunBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.ManualAsyncController", "TaskFactoryStartNewBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ManualTaskFactoryStartNewBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.ManualAsyncController", "ThreadStartBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ManualThreadStartBackgroundMethod");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetIoBoundNoSpecialAsync();
                    _fixture.GetIoBoundConfigureAwaitFalseAsync();
                    _fixture.GetCpuBoundTasksAsync();

                    _fixture.GetManualTaskRunBlocked();
                    _fixture.GetManualTaskFactoryStartNewBlocked();
                    _fixture.GetManualNewThreadStartBlocked();

                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assert.NotNull(metrics);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(_generalMetrics, metrics),
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(_ioBoundNoSpecialAsyncMetrics, metrics),
                () => Assertions.MetricsExist(_ioBoundConfigureAwaitFalseAsyncMetrics, metrics),
                () => Assertions.MetricsExist(_cpuBoundTasksAsyncMetrics, metrics)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(_manualTaskRunBlockedMetrics, metrics),
                () => Assertions.MetricsExist(_manualTaskFactoryStartNewMetrics, metrics),
                () => Assertions.MetricsExist(_manualNewThreadStartBlocked, metrics)
            );

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault(sample => sample.Path == "WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync");
            var expectedTransactionTraceSegments = new List<string>
            {
                @"DotNet/AsyncAwait/CpuBoundTasksAsync",
                @"TaskRunBackgroundMethod",
                @"TaskFactoryStartNewBackgroundMethod"
            };

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Apdex"},
            new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
            new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransaction", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", CallCountAllHarvests = ExpectedTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _ioBoundNoSpecialAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/AsyncAwait/IoBoundNoSpecialAsync"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/IoBoundNoSpecialAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _ioBoundConfigureAwaitFalseAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/IoBoundConfigureAwaitFalseAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _cpuBoundTasksAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/AsyncAwait/CpuBoundTasksAsync"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/CpuBoundTasksAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _manualTaskRunBlockedMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/ManualAsync/TaskRunBlocked"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/ManualAsync/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/ManualAsync/TaskRunBlocked", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskRunBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/TaskRunBlocked", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskRunBackgroundMethod", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskRunBlocked", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _manualTaskFactoryStartNewMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/ManualAsync/TaskFactoryStartNewBlocked"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskFactoryStartNewBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/TaskFactoryStartNewBlocked", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskFactoryStartNewBackgroundMethod", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _manualNewThreadStartBlocked = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/ManualAsync/NewThreadStartBlocked"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/ManualAsync/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/ManualAsync/NewThreadStartBlocked", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualThreadStartBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/NewThreadStartBlocked", metricScope = @"WebTransaction/WebAPI/ManualAsync/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualThreadStartBackgroundMethod", metricScope = @"WebTransaction/WebAPI/ManualAsync/NewThreadStartBlocked", callCount = 1 }
        };

    }
}
