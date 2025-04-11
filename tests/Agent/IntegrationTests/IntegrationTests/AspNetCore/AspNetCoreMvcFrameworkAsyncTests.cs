// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvcFrameworkAsyncTests : NewRelicIntegrationTest<AspNetCoreMvcFrameworkAsyncTestsFixture>
    {
        private readonly AspNetCoreMvcFrameworkAsyncTestsFixture _fixture;
        private const int ExpectedTransactionCount = 7;
        private const string AssemblyName = "AspNetCoreMvcFrameworkAsyncApplication";

        public AspNetCoreMvcFrameworkAsyncTests(AspNetCoreMvcFrameworkAsyncTestsFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitTestController", "CustomMethodAsync1", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync1");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitTestController", "CustomMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync2");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitTestController", "CustomMethodAsync3", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync3");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitTestController", "ConfigureAwaitFalseExampleAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitFalseExampleAsync");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitTestController", "ConfigureAwaitSubMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitSubMethodAsync2");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitTestController", "TaskRunBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskRunBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.AsyncAwaitTestController", "TaskFactoryStartNewBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskFactoryStartNewBackgroundMethod");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.ManualAsyncController", "TaskRunBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ManualTaskRunBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.ManualAsyncController", "TaskFactoryStartNewBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ManualTaskFactoryStartNewBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, AssemblyName, $"{AssemblyName}.Controllers.ManualAsyncController", "ThreadStartBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ManualThreadStartBackgroundMethod");

                },
                exerciseApplication: () =>
                {
                    _fixture.GetIoBoundNoSpecialAsync();
                    _fixture.GetCustomMiddlewareIoBoundNoSpecialAsync();
                    _fixture.GetIoBoundConfigureAwaitFalseAsync();
                    _fixture.GetCpuBoundTasksAsync();

                    _fixture.GetManualTaskRunBlocked();
                    _fixture.GetManualTaskFactoryStartNewBlocked();
                    _fixture.GetManualNewThreadStartBlocked();

                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assert.NotNull(metrics);

            Assertions.MetricsExist(_generalMetrics, metrics);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(_ioBoundNoSpecialAsyncMetrics, metrics),
                () => Assertions.MetricsExist(_customMiddlewareIoBoundNoSpecialAsyncMetrics, metrics),
                () => Assertions.MetricsExist(_ioBoundConfigureAwaitFalseAsyncMetrics, metrics),
                () => Assertions.MetricsExist(_cpuBoundTasksAsyncMetrics, metrics)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(_manualTaskRunBlockedMetrics, metrics),
                () => Assertions.MetricsExist(_manualTaskFactoryStartNewMetrics, metrics),
                () => Assertions.MetricsExist(_manualNewThreadStartBlocked, metrics)
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

            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", CallCountAllHarvests = ExpectedTransactionCount }
        };

        private readonly List<Assertions.ExpectedMetric> _ioBoundNoSpecialAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/AsyncAwaitTest/IoBoundNoSpecialAsync"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/AsyncAwaitTest/IoBoundNoSpecialAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwaitTestController/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwaitTestController/IoBoundNoSpecialAsync", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundNoSpecialAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _customMiddlewareIoBoundNoSpecialAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwaitTestController/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $@"DotNet/{AssemblyName}.CustomMiddleware/Invoke", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $@"DotNet/{AssemblyName}.CustomMiddleware/MiddlewareMethodAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwaitTestController/CustomMiddlewareIoBoundNoSpecialAsync", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $@"DotNet/{AssemblyName}.CustomMiddleware/Invoke", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $@"DotNet/{AssemblyName}.CustomMiddleware/MiddlewareMethodAsync", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _ioBoundConfigureAwaitFalseAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwaitTestController/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwaitTestController/IoBoundConfigureAwaitFalseAsync", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _cpuBoundTasksAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/AsyncAwaitTest/CpuBoundTasksAsync"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/AsyncAwaitTest/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/AsyncAwaitTest/CpuBoundTasksAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwaitTestController/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwaitTestController/CpuBoundTasksAsync", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", metricScope = @"WebTransaction/MVC/AsyncAwaitTest/CpuBoundTasksAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _manualTaskRunBlockedMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/ManualAsync/TaskRunBlocked"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/ManualAsync/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/ManualAsync/TaskRunBlocked", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsyncController/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskRunBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsyncController/TaskRunBlocked", metricScope = @"WebTransaction/MVC/ManualAsync/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/ManualAsync/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskRunBackgroundMethod", metricScope = @"WebTransaction/MVC/ManualAsync/TaskRunBlocked", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _manualTaskFactoryStartNewMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/ManualAsync/TaskFactoryStartNewBlocked"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsyncController/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskFactoryStartNewBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsyncController/TaskFactoryStartNewBlocked", metricScope = @"WebTransaction/MVC/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskFactoryStartNewBackgroundMethod", metricScope = @"WebTransaction/MVC/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _manualNewThreadStartBlocked = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/ManualAsync/NewThreadStartBlocked"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/ManualAsync/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/ManualAsync/NewThreadStartBlocked", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsyncController/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualThreadStartBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsyncController/NewThreadStartBlocked", metricScope = @"WebTransaction/MVC/ManualAsync/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/ManualAsync/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ManualThreadStartBackgroundMethod", metricScope = @"WebTransaction/MVC/ManualAsync/NewThreadStartBlocked", callCount = 1 }
        };

    }
}
