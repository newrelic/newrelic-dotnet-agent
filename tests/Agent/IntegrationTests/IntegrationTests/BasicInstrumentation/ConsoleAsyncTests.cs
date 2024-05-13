// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class ConsoleAsyncTests : NewRelicIntegrationTest<ConsoleAsyncFixture>
    {
        private readonly ConsoleAsyncFixture _fixture;
        private const int ExpectedTransactionCount = 7;

        public ConsoleAsyncTests(ConsoleAsyncFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {

                    _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");

                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "IoBoundNoSpecialAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync", "IoBoundNoSpecialAsync", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "IoBoundConfigureAwaitFalseAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync", "IoBoundConfigureAwaitFalseAsync", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "CpuBoundTasksAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync", "CpuBoundTasksAsync", 7);

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "CustomMethodAsync1", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync1");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "CustomMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync2");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "CustomMethodAsync3", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync3");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "ConfigureAwaitFalseExampleAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitFalseExampleAsync");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "ConfigureAwaitSubMethodAsync2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ConfigureAwaitSubMethodAsync2");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "TaskRunBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskRunBackgroundMethod");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.AsyncAwaitUseCases", "TaskFactoryStartNewBackgroundMethod", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "TaskFactoryStartNewBackgroundMethod");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.ManualAsyncUseCases", "TaskRunBlocked", "MultithreadedTrackingWrapper", "TaskRunBlocked", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.ManualAsyncUseCases", "TaskFactoryStartNewBlocked", "MultithreadedTrackingWrapper", "TaskFactoryStartNewBlocked", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.ManualAsyncUseCases", "NewThreadStartBlocked", "MultithreadedTrackingWrapper", "NewThreadStartBlocked", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "ConsoleAsyncApplication", "ConsoleAsyncApplication.ManualAsyncUseCases", "MultipleThreadSegmentParenting", "MultithreadedTrackingWrapper", "MultipleThreadSegmentParenting", 7);
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
                () => Assertions.MetricsExist(_cpuBoundTasksAsyncMetrics, metrics),
                () => Assertions.MetricsExist(_manualAsyncTaskRunBlockedMetrics, metrics),
                () => Assertions.MetricsExist(_manualAsyncTaskFactoryStartNewBlockedMetrics, metrics),
                () => Assertions.MetricsExist(_manualAsyncNewThreadStartBlockedMetrics, metrics),
                () => Assertions.MetricsExist(_manualAsyncMultipleThreadSegmentParentingMetrics, metrics)
            );

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            Assert.NotNull(transactionSample);

            var actualTraceTree = transactionSample.TraceData.RootSegment;

            var expectedTraceTree = ExpectedTransactionTraceSegment.NewTree("ConsoleAsyncApplication.ManualAsyncUseCases", "MultipleThreadSegmentParenting",
                ExpectedTransactionTraceSegment.NewSubtree("MultipleThreadSegmentParenting", "ConsoleAsyncApplication.ManualAsyncUseCases", "MultipleThreadSegmentParenting",
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task1", "ConsoleAsyncApplication.ManualAsyncUseCases", "Task1",
                        ExpectedTransactionTraceSegment.NewSubtree("DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task1SubMethod1", "ConsoleAsyncApplication.ManualAsyncUseCases", "Task1SubMethod1"),
                        ExpectedTransactionTraceSegment.NewSubtree("DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task1SubMethod2", "ConsoleAsyncApplication.ManualAsyncUseCases", "Task1SubMethod2")
                    ),
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task2", "ConsoleAsyncApplication.ManualAsyncUseCases", "Task2",
                        ExpectedTransactionTraceSegment.NewSubtree("DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task2SubMethod1", "ConsoleAsyncApplication.ManualAsyncUseCases", "Task2SubMethod1"),
                        ExpectedTransactionTraceSegment.NewSubtree("DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task2SubMethod2", "ConsoleAsyncApplication.ManualAsyncUseCases", "Task2SubMethod2")
                    ),
                    ExpectedTransactionTraceSegment.NewBackgroundThreadSubtree("DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task3", "ConsoleAsyncApplication.ManualAsyncUseCases", "Task3",
                        ExpectedTransactionTraceSegment.NewSubtree("DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task3SubMethod1", "ConsoleAsyncApplication.ManualAsyncUseCases", "Task3SubMethod1"),
                        ExpectedTransactionTraceSegment.NewSubtree("DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task3SubMethod2", "ConsoleAsyncApplication.ManualAsyncUseCases", "Task3SubMethod2")
                    )
                )
            );

            Assertions.TransactionTraceSegmentTreeEquals(expectedTraceTree, actualTraceTree);
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
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

        private readonly List<Assertions.ExpectedMetric> _manualAsyncTaskRunBlockedMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime/Custom/TaskRunBlocked", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/TaskRunBackgroundMethod", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/TaskRunBackgroundSubMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/TaskRunBlocked", metricScope = @"OtherTransaction/Custom/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/TaskRunBackgroundMethod", metricScope = @"OtherTransaction/Custom/TaskRunBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/TaskRunBackgroundSubMethod", metricScope = @"OtherTransaction/Custom/TaskRunBlocked", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _manualAsyncTaskFactoryStartNewBlockedMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime/Custom/TaskFactoryStartNewBlocked", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/TaskFactoryStartNewBackgroundMethod", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/TaskFactoryStartNewBackgroundSubMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/TaskFactoryStartNewBlocked", metricScope = @"OtherTransaction/Custom/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/TaskFactoryStartNewBackgroundMethod", metricScope = @"OtherTransaction/Custom/TaskFactoryStartNewBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/TaskFactoryStartNewBackgroundSubMethod", metricScope = @"OtherTransaction/Custom/TaskFactoryStartNewBlocked", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _manualAsyncNewThreadStartBlockedMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime/Custom/NewThreadStartBlocked", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/ThreadStartBackgroundMethod", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/ThreadStartBackgroundSubMethod", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/NewThreadStartBlocked", metricScope = @"OtherTransaction/Custom/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/ThreadStartBackgroundMethod", metricScope = @"OtherTransaction/Custom/NewThreadStartBlocked", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/ThreadStartBackgroundSubMethod", metricScope = @"OtherTransaction/Custom/NewThreadStartBlocked", callCount = 1 },
        };

        private readonly List<Assertions.ExpectedMetric> _manualAsyncMultipleThreadSegmentParentingMetrics = new List<Assertions.ExpectedMetric> {
            new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime/Custom/MultipleThreadSegmentParenting", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task1", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task2", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task3", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task1SubMethod1", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task1SubMethod2", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task2SubMethod1", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task2SubMethod2", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task3SubMethod1", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task3SubMethod2", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"Custom/MultipleThreadSegmentParenting", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task1", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task2", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task3", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task1SubMethod1", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task1SubMethod2", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task2SubMethod1", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task2SubMethod2", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task3SubMethod1", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ConsoleAsyncApplication.ManualAsyncUseCases/Task3SubMethod2", metricScope = @"OtherTransaction/Custom/MultipleThreadSegmentParenting", callCount = 1 },
        };
    }
}
