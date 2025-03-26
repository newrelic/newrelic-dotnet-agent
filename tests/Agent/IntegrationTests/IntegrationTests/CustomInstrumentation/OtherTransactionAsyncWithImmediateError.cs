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
    public class OtherTransactionAsyncWithError : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public OtherTransactionAsyncWithError(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();

                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationAsyncController", "CustomMethodBackgroundThreadWithError", "AsyncForceNewTransactionWrapper");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetBackgroundThreadWithError();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/BasicMvcApplication.Controllers.CustomInstrumentationAsyncController/CustomMethodBackgroundThreadWithError", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationAsyncController/CustomMethodBackgroundThreadWithError", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationAsyncController/CustomMethodBackgroundThreadWithError", metricScope = "OtherTransaction/Custom/BasicMvcApplication.Controllers.CustomInstrumentationAsyncController/CustomMethodBackgroundThreadWithError", callCount = 1 }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"DotNet/BasicMvcApplication.Controllers.CustomInstrumentationAsyncController/CustomMethodBackgroundThreadWithError",
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog
                .GetTransactionSamples()
                .FirstOrDefault(sample => sample.Path == @"OtherTransaction/Custom/BasicMvcApplication.Controllers.CustomInstrumentationAsyncController/CustomMethodBackgroundThreadWithError");

            var transactionEvent = _fixture.AgentLog.GetTransactionEvents()
                .FirstOrDefault();
            var tracedError = _fixture.AgentLog.TryGetErrorTrace("OtherTransaction/Custom/BasicMvcApplication.Controllers.CustomInstrumentationAsyncController/CustomMethodBackgroundThreadWithError");

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent),
                () => Assert.NotNull(tracedError)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
