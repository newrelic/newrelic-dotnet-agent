// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.RestSharp
{
    [NetFrameworkTest]
    public class RestSharpInstrumentationDistributedTracing : IClassFixture<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public RestSharpInstrumentationDistributedTracing(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.GetRestSharpSyncClient(method: "GET", generic: false);
                    _fixture.GetRestSharpSyncClient(method: "PUT", generic: false);
                    _fixture.GetRestSharpSyncClient(method: "POST", generic: false);
                    _fixture.GetRestSharpSyncClient(method: "DELETE", generic: false);
                    _fixture.GetRestSharpClientTaskCancelled();

                    //Adding some time for metrics to be fully generated. 
                    Thread.Sleep(3000);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var myHostname = _fixture.DestinationServerName; // This is needed because we are making "external" calls to ourself to test RestSharp
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {

                new Assertions.ExpectedMetric { metricName = "External/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = "External/allWeb", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/GET", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/PUT", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/POST", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/DELETE", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/GET", metricScope = @"WebTransaction/MVC/RestSharpController/RestSharpClientTaskCancelled", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/GET", metricScope = @"WebTransaction/MVC/RestSharpController/SyncClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/PUT", metricScope = @"WebTransaction/MVC/RestSharpController/SyncClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/POST", metricScope = @"WebTransaction/MVC/RestSharpController/SyncClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"External/{myHostname}/Stream/DELETE", metricScope = @"WebTransaction/MVC/RestSharpController/SyncClient", callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/MVC/RestSharpController/SyncClient" || sample.Path == @"WebTransaction/WebAPI/RestAPI/Get" || sample.Path == @"WebTransaction/MVC/RestSharpController/RestSharpClientTaskCancelled")
                .FirstOrDefault();

            Assert.NotNull(transactionSample);

            var transactionEventWithExternal = _fixture.AgentLog.GetTransactionEvents()
                .Where(e => e.IntrinsicAttributes.ContainsKey("externalDuration"))
                .FirstOrDefault();

            var externalSpanEvents = _fixture.AgentLog.GetSpanEvents()
                .Where(e => e.AgentAttributes.ContainsKey("http.url"))
                .ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.NotNull(transactionEventWithExternal),
                () => Assert.All(externalSpanEvents, AssertSpanEventsContainHttpStatusCodeForCompletedRequests)
            );

            var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";
            var wrapperError = _fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex);

            Assert.Null(wrapperError);

            void AssertSpanEventsContainHttpStatusCodeForCompletedRequests(SpanEvent spanEvent)
            {
                var url = (string)spanEvent.AgentAttributes["http.url"];
                if (url.Contains("/RestAPI/4"))
                {
                    Assert.DoesNotContain("http.statusCode", spanEvent.AgentAttributes.Keys);
                }
                else
                {
                    Assert.Contains("http.statusCode", spanEvent.AgentAttributes.Keys);
                }
            }
        }
    }
}
