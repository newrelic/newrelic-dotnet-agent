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
    public class CustomInstrumentationAsync : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public CustomInstrumentationAsync(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
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

                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentationAsync.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationAsyncController", "CustomMethodDefaultWrapperAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "MyCustomMetricName", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationAsyncController", "CustomSegmentTransactionSegmentWrapper", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.CustomSegmentWrapperAsync");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationAsyncController", "CustomSegmentAlternateParameterNamingTheSegment", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.CustomSegmentWrapperAsync");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetCustomInstrumentationAsync();
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
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/AsyncCustomSegmentName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/AsyncCustomSegmentNameAlternate", callCount = 1 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"Custom/AsyncCustomSegmentName", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/AsyncCustomSegmentNameAlternate", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"MyCustomMetricName",
                @"AsyncCustomSegmentName",
                @"AsyncCustomSegmentNameAlternate"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog
                .GetTransactionSamples()
                .FirstOrDefault(sample => sample.Path == @"WebTransaction/Custom/MyCustomMetricName");

            var transactionEvent = _fixture.AgentLog.GetTransactionEvents()
                .FirstOrDefault();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
