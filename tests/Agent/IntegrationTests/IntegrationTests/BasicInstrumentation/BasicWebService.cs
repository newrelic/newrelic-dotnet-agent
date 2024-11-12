// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class BasicWebService : NewRelicIntegrationTest<RemoteServiceFixtures.BasicWebService>
    {

        private readonly RemoteServiceFixtures.BasicWebService _fixture;

        public BasicWebService(RemoteServiceFixtures.BasicWebService fixture, ITestOutputHelper output) : base(fixture)
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
                },
                exerciseApplication: () =>
                {
                    _fixture.InvokeServiceHttp();
                    _fixture.InvokeServiceSoap();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"DotNet/System.Web.Services.Protocols.SyncSessionlessHandler/ProcessRequest", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric {metricName = @"DotNet/System.Web.Services.Protocols.SyncSessionlessHandler/ProcessRequest", metricScope = "WebTransaction/WebService/BasicWebService.TestWebService.HelloWorld", CallCountAllHarvests = 2},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/WebService/BasicWebService.TestWebService.HelloWorld", CallCountAllHarvests = 2},
                new Assertions.ExpectedMetric {metricName = @"DotNet/BasicWebService.TestWebService.HelloWorld", CallCountAllHarvests = 2},
                new Assertions.ExpectedMetric {metricName = @"DotNet/BasicWebService.TestWebService.HelloWorld", metricScope = "WebTransaction/WebService/BasicWebService.TestWebService.HelloWorld", CallCountAllHarvests = 2}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"DotNet/System.Web.Services.Protocols.SyncSessionlessHandler/ProcessRequest",
                @"BasicWebService.TestWebService.HelloWorld"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/WebService/BasicWebService.TestWebService.HelloWorld")
                .FirstOrDefault();
            Assert.NotNull(transactionSample);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );
        }
    }
}
