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
    public class OtherTransaction : NewRelicIntegrationTest<RemoteServiceFixtures.AgentApiExecutor>
    {
        private readonly RemoteServiceFixtures.AgentApiExecutor _fixture;

        public OtherTransaction(RemoteServiceFixtures.AgentApiExecutor fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);

                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    //Use the two different wrappers here to ensure that they both filter to OtherTransactionWrapper and create the same behavior

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor", "NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor.Program", "RealMain", "NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper", "MyCustomMetricName");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor", "NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor.Program", "SomeSlowMethod", "NewRelic.Agent.Core.Tracer.Factories.BackgroundThreadTracerFactory");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Custom/MyMetric", callCount = 1 },

				// Transaction metric
				new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/MyCustomMetricName", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomMetricName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomMetricName", metricScope = "OtherTransaction/Custom/MyCustomMetricName",  callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor.Program/SomeSlowMethod", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor.Program/SomeSlowMethod", metricScope = "OtherTransaction/Custom/MyCustomMetricName",  callCount = 1 }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"MyCustomMetricName",
                @"DotNet/NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor.Program/SomeSlowMethod"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"OtherTransaction/Custom/MyCustomMetricName")
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
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
