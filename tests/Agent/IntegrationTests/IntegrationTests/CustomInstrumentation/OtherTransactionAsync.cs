// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetFrameworkTest]
    public class OtherTransactionAsync : IClassFixture<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {

        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public OtherTransactionAsync(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\CustomInstrumentation.xml";

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationAsyncController", "CustomMethodBackgroundThread", "AsyncForceNewTransactionWrapper", "MyCustomMetricName");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetBackgroundThread();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomMetricName", metricScope = "OtherTransaction/Custom/MyCustomMetricName", callCount = 1 }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"MyCustomMetricName",
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog
                .GetTransactionSamples()
                .FirstOrDefault(sample => sample.Path == @"OtherTransaction/Custom/MyCustomMetricName");

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
