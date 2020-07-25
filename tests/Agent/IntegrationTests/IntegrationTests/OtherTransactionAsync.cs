using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class OtherTransactionAsync : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public OtherTransactionAsync(RemoteServiceFixtures.BasicMvcApplication fixture, ITestOutputHelper output)
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

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationAsyncController", "CustomMethodBackgroundThread", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync", "MyCustomMetricName", 7);
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
