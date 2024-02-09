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

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetCoreTest]
    public class NetCoreAsyncTests : NewRelicIntegrationTest<NetCoreAsyncTestsFixture>
    {
        private readonly NetCoreAsyncTestsFixture _fixture;
        private const int ExpectedTransactionCount = 3;

        public NetCoreAsyncTests(NetCoreAsyncTestsFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "IoBoundNoSpecialAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync", "IoBoundNoSpecialAsync", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "IoBoundConfigureAwaitFalseAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync", "IoBoundConfigureAwaitFalseAsync", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "CpuBoundTasksAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync", "CpuBoundTasksAsync", 7);

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "CustomMethodAsync1", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync1");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "CustomMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync2");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "CustomMethodAsync3", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync3");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "ConfigureAwaitFalseExampleAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitFalseExampleAsync");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "ConfigureAwaitSubMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitSubMethodAsync2");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "TaskRunBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskRunBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", "NetCoreAsyncApplication.AsyncUseCases", "TaskFactoryStartNewBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskFactoryStartNewBackgroundMethod");

                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.DisableEventListenerSamplers(); // Required for .NET 8 to pass.
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assert.NotNull(metrics);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(_generalMetrics, metrics),
                () => Assertions.MetricsExist(_ioBoundNoSpecialAsyncMetrics, metrics),
                () => Assertions.MetricsExist(_ioBoundConfigureAwaitFalseAsyncMetrics, metrics),
                () => Assertions.MetricsExist(_cpuBoundTasksAsyncMetrics, metrics)
            );
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/OS/Linux", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime", callCount = ExpectedTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _ioBoundNoSpecialAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime/Custom/IoBoundNoSpecialAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/IoBoundNoSpecialAsync", metricScope = @"OtherTransaction/Custom/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync1", metricScope = @"OtherTransaction/Custom/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync2", metricScope = @"OtherTransaction/Custom/IoBoundNoSpecialAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/CustomMethodAsync3", metricScope = @"OtherTransaction/Custom/IoBoundNoSpecialAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _ioBoundConfigureAwaitFalseAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime/Custom/IoBoundConfigureAwaitFalseAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/IoBoundConfigureAwaitFalseAsync", metricScope = @"OtherTransaction/Custom/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitFalseExampleAsync", metricScope = @"OtherTransaction/Custom/IoBoundConfigureAwaitFalseAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/ConfigureAwaitSubMethodAsync2", metricScope = @"OtherTransaction/Custom/IoBoundConfigureAwaitFalseAsync", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _cpuBoundTasksAsyncMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime/Custom/CpuBoundTasksAsync", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/CpuBoundTasksAsync", metricScope = @"OtherTransaction/Custom/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBackgroundMethod", metricScope = @"OtherTransaction/Custom/CpuBoundTasksAsync", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBackgroundMethod", metricScope = @"OtherTransaction/Custom/CpuBoundTasksAsync", callCount = 1 }
        };
    }
}
