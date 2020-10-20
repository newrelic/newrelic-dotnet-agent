// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class MvcAsyncTests : IClassFixture<MvcAsyncFixture>
    {
        private readonly MvcAsyncFixture _fixture;
        private const int ExpectedTransactionCount = 3;
        private const string AssemblyName = "MvcAsyncApplication";
        private const string MetricNameFrameworkName = "MVC";
        private const string MetricNameAsyncAwaitControllerName = "AsyncAwaitController";

        public MvcAsyncTests(MvcAsyncFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\CustomInstrumentation.xml";

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

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
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

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault(sample => sample.Path == $"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync");
            var expectedTransactionTraceSegments = new List<string>
            {
                $@"DotNet/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync",
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
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Apdex"},
            new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
            new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = ExpectedTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _ioBoundNoSpecialAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = $@"Apdex/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundNoSpecialAsync"},
            new Assertions.ExpectedMetric { metricName = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $@"WebTransactionTotalTime/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundNoSpecialAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = $@"DotNet/{MetricNameAsyncAwaitControllerName}/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = $@"DotNet/{MetricNameAsyncAwaitControllerName}/IoBoundNoSpecialAsync", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundNoSpecialAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _ioBoundConfigureAwaitFalseAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = $@"Apdex/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundConfigureAwaitFalseAsync"},
            new Assertions.ExpectedMetric { metricName = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $@"WebTransactionTotalTime/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundConfigureAwaitFalseAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = $@"DotNet/{MetricNameAsyncAwaitControllerName}/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = $@"DotNet/{MetricNameAsyncAwaitControllerName}/IoBoundConfigureAwaitFalseAsync", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/IoBoundConfigureAwaitFalseAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _cpuBoundTasksAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = $@"Apdex/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync"},
            new Assertions.ExpectedMetric { metricName = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $@"WebTransactionTotalTime/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = $@"DotNet/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = $@"DotNet/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", metricScope = $@"WebTransaction/{MetricNameFrameworkName}/{MetricNameAsyncAwaitControllerName}/CpuBoundTasksAsync", callCount = 1 }
        };
    }
}
