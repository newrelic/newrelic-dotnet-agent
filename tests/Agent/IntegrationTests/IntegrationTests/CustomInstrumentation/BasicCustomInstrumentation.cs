// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetFrameworkTest]
    public class BasicCustomInstrumentation : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public BasicCustomInstrumentation(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController", "CustomMethodDefaultWrapper", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomMetricName", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController", "CustomMethodDefaultTracer", "NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController", "CustomMethodUnknownWrapperName", "INVALID.WRAPPER.NAME");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController", "CustomSegmentTransactionSegmentWrapper", "NewRelic.Providers.Wrapper.CustomInstrumentation.CustomSegmentWrapper");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController",
                        "CustomSegmentAlternateParameterNamingTheSegment", "NewRelic.Providers.Wrapper.CustomInstrumentation.CustomSegmentWrapper");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController", "CustomSegmentTracer", "NewRelic.Agent.Core.Tracer.Factories.CustomSegmentTracerFactory");


                    // NOTE: If no wrapperName (a.k.a. "tracerFactoryName") is specified in instrumentation then the profiler will automatically use "NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory". This is hard-coded into the profiler.
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController", "CustomMethodNoWrapperName");

                    // Ignored transactions
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController", "CustomMethodIgnoreTransactionWrapper", "NewRelic.Providers.Wrapper.CustomInstrumentation.IgnoreTransactionWrapper");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController", "CustomMethodIgnoreTransactionTracerFactory", "NewRelic.Agent.Core.Tracer.Factories.IgnoreTransactionTracerFactory");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationController", "CustomMethodIgnoreTransactionWrapperAsync", "NewRelic.Providers.Wrapper.CustomInstrumentation.IgnoreTransactionWrapper");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetCustomInstrumentation();
                    _fixture.GetIgnoredByIgnoreTransactionWrapper();
                    _fixture.GetIgnoredByIgnoreTransactionTracerFactory();
                    _fixture.GetIgnoredByIgnoreTransactionWrapperAsync();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomMetricName", callCount = 1 },

				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationController/CustomMethodDefaultTracer", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationController/CustomMethodNoWrapperName", callCount = 1 },
				
				// Scoped
				new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomMetricName", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationController/CustomMethodDefaultTracer", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationController/CustomMethodNoWrapperName", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"Custom/CustomSegmentName", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/AlternateCustomSegmentName", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/CustomSegmentNameFromTracer", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 }

            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// An unrecognized wrapperName will result in no tracer being selected.
				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationController/CustomMethodUnknownWrapperName", callCount = 1 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationController/CustomMethodUnknownWrapperName", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 },

				// Ignored transactions
				new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomMetricNameIgnoredByIgnoreTransactionWrapper", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomMetricNameIgnoredByIgnoreTransactionTracerFactory", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomMetricNameIgnoredByIgnoreTransactionWrapperAsync", callCount = 1 }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                @"MyCustomMetricName",
                @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationController/CustomMethodDefaultTracer",
                @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationController/CustomMethodNoWrapperName",
                @"CustomSegmentName",
                @"AlternateCustomSegmentName",
                @"CustomSegmentNameFromTracer"
            };
            var unexpectedTransactionTraceSegments = new List<string>
            {
				// An unrecognized wrapperName will result in no tracer being selected.
				@"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationController/CustomMethodUnknownWrapperName"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/Custom/MyCustomMetricName")
                .FirstOrDefault();
            var transactionEvent = _fixture.AgentLog.GetTransactionEvents()
                .FirstOrDefault();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceSegmentsNotExist(unexpectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
