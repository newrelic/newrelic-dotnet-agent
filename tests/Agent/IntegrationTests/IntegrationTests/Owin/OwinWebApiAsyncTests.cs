// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using Xunit;
using Xunit.Abstractions;
using System.IO;

namespace NewRelic.Agent.IntegrationTests.Owin
{
    [NetFrameworkTest]
    public abstract class OwinWebApiAsyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : OwinWebApiFixture
    {
        private readonly OwinWebApiFixture _fixture;
        private const int ExpectedTransactionCount = 8;

        // The base test class runs tests for Owin 2; the derived classes test Owin 3 and 4
        protected OwinWebApiAsyncTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            var assemblyName = _fixture.AssemblyName;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(30);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(30);
                    configModifier.ConfigureFasterErrorTracesHarvestCycle(30);

                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.AsyncAwaitController", "CustomMethodAsync1", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync1");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.AsyncAwaitController", "CustomMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync2");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.AsyncAwaitController", "CustomMethodAsync3", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync3");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.AsyncAwaitController", "ConfigureAwaitFalseExampleAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitFalseExampleAsync");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.AsyncAwaitController", "ConfigureAwaitSubMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitSubMethodAsync2");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.AsyncAwaitController", "TaskRunBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskRunBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.AsyncAwaitController", "TaskFactoryStartNewBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskFactoryStartNewBackgroundMethod");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.ManualAsyncController", "TaskRunBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ManualTaskRunBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.ManualAsyncController", "TaskFactoryStartNewBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ManualTaskFactoryStartNewBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, assemblyName, $"{assemblyName}.Controllers.ManualAsyncController", "ThreadStartBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ManualThreadStartBackgroundMethod");

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

                    _fixture.ErrorResponse();

                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var AssemblyName = _fixture.AssemblyName;

            var generalMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"Apdex"},
                new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = ExpectedTransactionCount },

                new Assertions.ExpectedMetric { metricName = @"DotNet/Owin Middleware Pipeline", CallCountAllHarvests = ExpectedTransactionCount },
            };

            var ioBoundNoSpecialAsyncMetrics = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/AsyncAwait/IoBoundNoSpecialAsync"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/IoBoundNoSpecialAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Owin Middleware Pipeline", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundNoSpecialAsync", callCount = 1 }
            };

            var customMiddlewareIoBoundNoSpecialAsyncMetrics = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"DotNet/{AssemblyName}.CustomMiddleware/Invoke", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"DotNet/{AssemblyName}.CustomMiddleware/MiddlewareMethodAsync", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Owin Middleware Pipeline", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"DotNet/{AssemblyName}.CustomMiddleware/Invoke", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"DotNet/{AssemblyName}.CustomMiddleware/MiddlewareMethodAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync", callCount = 1 }
            };

            var ioBoundConfigureAwaitFalseAsyncMetrics = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/IoBoundConfigureAwaitFalseAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Owin Middleware Pipeline", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", metricScope = @"WebTransaction/WebAPI/AsyncAwait/IoBoundConfigureAwaitFalseAsync", callCount = 1 }
            };

            var cpuBoundTasksAsyncMetrics = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/AsyncAwait/CpuBoundTasksAsync"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncAwait/CpuBoundTasksAsync", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Owin Middleware Pipeline", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", metricScope = @"WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync", callCount = 1 }
            };

            var errorReponse = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/AsyncAwait/ErrorResponse"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/AsyncAwait/ErrorResponse", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/AsyncAwait/ErrorResponse", callCount = 1 },
            };

            var manualTaskRunBlockedMetrics = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/ManualAsync/TaskRunBlocked"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/ManualAsync/TaskRunBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/ManualAsync/TaskRunBlocked", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/TaskRunBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskRunBackgroundMethod", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/TaskRunBlocked", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskRunBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Owin Middleware Pipeline", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskRunBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskRunBackgroundMethod", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskRunBlocked", callCount = 1 },
            };

            var manualTaskFactoryStartNewMetrics = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/ManualAsync/TaskFactoryStartNewBlocked"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskFactoryStartNewBackgroundMethod", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/TaskFactoryStartNewBlocked", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Owin Middleware Pipeline", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ManualTaskFactoryStartNewBackgroundMethod", metricScope = @"WebTransaction/WebAPI/ManualAsync/TaskFactoryStartNewBlocked", callCount = 1 },
            };

            var manualNewThreadStartBlocked = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Apdex/WebAPI/ManualAsync/NewThreadStartBlocked"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WebAPI/ManualAsync/NewThreadStartBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/WebAPI/ManualAsync/NewThreadStartBlocked", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/NewThreadStartBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ManualThreadStartBackgroundMethod", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/ManualAsync/NewThreadStartBlocked", metricScope = @"WebTransaction/WebAPI/ManualAsync/NewThreadStartBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Owin Middleware Pipeline", metricScope = @"WebTransaction/WebAPI/ManualAsync/NewThreadStartBlocked", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/ManualThreadStartBackgroundMethod", metricScope = @"WebTransaction/WebAPI/ManualAsync/NewThreadStartBlocked", callCount = 1 }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assert.NotNull(metrics);

            Assertions.MetricsExist(generalMetrics, metrics);

            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();

            Assert.Single(errorTraces);
            Assert.Single(errorEvents);

            var errorTrace = errorTraces.First();
            var errorEvent = errorEvents.First();

            Assert.Equal("500", errorTrace.ExceptionClassName);
            Assert.Equal("500", errorEvent.IntrinsicAttributes["error.class"]);


            Assertions.MetricsExist(ioBoundNoSpecialAsyncMetrics, metrics);
            Assertions.MetricsExist(ioBoundConfigureAwaitFalseAsyncMetrics, metrics);
            Assertions.MetricsExist(cpuBoundTasksAsyncMetrics, metrics);
            Assertions.MetricsExist(errorReponse, metrics);

            Assertions.MetricsExist(customMiddlewareIoBoundNoSpecialAsyncMetrics, metrics);


            Assertions.MetricsExist(manualTaskRunBlockedMetrics, metrics);
            Assertions.MetricsExist(manualTaskFactoryStartNewMetrics, metrics);
            Assertions.MetricsExist(manualNewThreadStartBlocked, metrics);

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault(sample => sample.Path == "WebTransaction/WebAPI/AsyncAwait/CpuBoundTasksAsync");
            var expectedTransactionTraceSegments = new List<string>
            {
                @"Owin Middleware Pipeline",
                @"DotNet/AsyncAwait/CpuBoundTasksAsync",
                @"TaskRunBackgroundMethod",
                @"TaskFactoryStartNewBackgroundMethod"
            };
            var expectedAttributes = new Dictionary<string, string>
            {
                { "request.uri", "/AsyncAwait/CpuBoundTasksAsync" },
            };

            Assert.NotNull(transactionSample);
            Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample);
            Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.Agent, transactionSample);
        }
    }

    public class OwinWebApiAsyncTests : OwinWebApiAsyncTestsBase<OwinWebApiFixture>
    {
        public OwinWebApiAsyncTests(OwinWebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class Owin3WebApiAsyncTests : OwinWebApiAsyncTestsBase<Owin3WebApiFixture>
    {
        public Owin3WebApiAsyncTests(Owin3WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class Owin4WebApiAsyncTests : OwinWebApiAsyncTestsBase<Owin4WebApiFixture>
    {
        public Owin4WebApiAsyncTests(Owin4WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
