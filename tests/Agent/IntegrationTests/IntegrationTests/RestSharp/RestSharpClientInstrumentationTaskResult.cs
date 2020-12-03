// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.RestSharp
{
    [NetFrameworkTest]
    public class RestSharpInstrumentationTaskResult : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public RestSharpInstrumentationTaskResult(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.GetRestSharpTaskResultClient(method: "GET", generic: true, cancelable: true);
                    _fixture.GetRestSharpTaskResultClient(method: "PUT", generic: false, cancelable: false);
                    _fixture.GetRestSharpTaskResultClient(method: "POST", generic: false, cancelable: false);
                    _fixture.GetRestSharpTaskResultClient(method: "DELETE", generic: true, cancelable: true);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var myHostname = _fixture.DestinationServerName; // This is needed because we are making "external" calls to ourself to test RestSharp
            var crossProcessId = _fixture.AgentLog.GetCrossProcessId();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "External/all", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = "External/allWeb", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/PUT", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/POST", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/DELETE", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"ExternalTransaction/{myHostname}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Get", metricScope = @"WebTransaction/MVC/RestSharpController/TaskResultClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"ExternalTransaction/{myHostname}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Put", metricScope = @"WebTransaction/MVC/RestSharpController/TaskResultClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"ExternalTransaction/{myHostname}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Post", metricScope = @"WebTransaction/MVC/RestSharpController/TaskResultClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"ExternalTransaction/{myHostname}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Delete", metricScope = @"WebTransaction/MVC/RestSharpController/TaskResultClient", callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/MVC/RestSharpController/TaskResultClient" || sample.Path == @"WebTransaction/WebAPI/RestAPI/Get")
                .FirstOrDefault();

            Assert.NotNull(transactionSample);

            var transactionEventWithExternal = _fixture.AgentLog.GetTransactionEvents()
                .Where(e => e.IntrinsicAttributes.ContainsKey("externalDuration"))
                .FirstOrDefault();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.NotNull(transactionEventWithExternal)
            );

            var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";
            var wrapperError = _fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex);

            Assert.Null(wrapperError);
        }
    }
}
