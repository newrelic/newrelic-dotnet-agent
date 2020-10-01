// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.HttpClientInstrumentation.NetCore
{
    [NetCoreTest]
    public class HttpClientInstrumentationNetCore : IClassFixture<AspNetCoreMvcBasicRequestsFixture>
    {
        private readonly AspNetCoreMvcBasicRequestsFixture _fixture;

        public HttpClientInstrumentationNetCore(AspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
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
                    _fixture.GetHttpClient();
                    _fixture.GetHttpClientTaskCancelled();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"External/all", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"External/allWeb", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.bing.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.bing.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET", metricScope = @"WebTransaction/MVC/Home/HttpClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/Stream/GET", metricScope = @"WebTransaction/MVC/Home/HttpClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.bing.com/Stream/GET", metricScope = @"WebTransaction/MVC/Home/HttpClientTaskCancelled", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/SpanEvent/TotalEventsSeen", CallCountAllHarvests = 9 }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"External/www.google.com/Stream/GET",
                @"External/www.yahoo.com/Stream/GET"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/MVC/Home/HttpClient")
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
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assert.NotNull(transactionEventWithExternal),
                () => Assert.All(externalSpanEvents, AssertSpanEventsContainHttpStatusCodeForCompletedRequests)
            );

            var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";
            var wrapperError = _fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex);

            Assert.Null(wrapperError);

            void AssertSpanEventsContainHttpStatusCodeForCompletedRequests(SpanEvent spanEvent)
            {
                var url = (string)spanEvent.AgentAttributes["http.url"];
                if (url.Contains("bing"))
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
