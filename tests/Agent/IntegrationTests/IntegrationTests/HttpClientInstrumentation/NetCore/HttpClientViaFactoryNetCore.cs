// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.HttpClientInstrumentation.NetCore
{
    public class HttpClientViaFactoryNetCore : NewRelicIntegrationTest<AspNetCoreMvcBasicRequestsFixture>
    {
        private readonly AspNetCoreMvcBasicRequestsFixture _fixture;

        public HttpClientViaFactoryNetCore(AspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
            : base(fixture)
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
                    _fixture.GetHttpClientFactory();
                    _fixture.GetTypedHttpClient();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"External/all", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"External/allWeb", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"External/download.newrelic.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/docs.newrelic.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/download.newrelic.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/docs.newrelic.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/download.newrelic.com/Stream/GET", metricScope = @"WebTransaction/MVC/Home/TypedHttpClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/docs.newrelic.com/Stream/GET", metricScope = @"WebTransaction/MVC/Home/HttpClientFactory", callCount = 1 }
            };

            var expectedTransactionTraceSegments = new Dictionary<string, List<string>>
            {
                { "WebTransaction/MVC/Home/HttpClientFactory", new List<string> {@"External/docs.newrelic.com/Stream/GET"} },
                { "WebTransaction/MVC/Home/TypedHttpClient", new List<string> {@"External/download.newrelic.com/Stream/GET"} }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog
                .GetTransactionSamples()
                .FirstOrDefault(sample => expectedTransactionTraceSegments.ContainsKey(sample.Path));

            Assert.NotNull(transactionSample);

            var transactionEventWithExternal = _fixture.AgentLog
                .GetTransactionEvents()
                .FirstOrDefault(e => e.IntrinsicAttributes.ContainsKey("externalDuration"));

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments[transactionSample.Path], transactionSample),
                () => Assert.NotNull(transactionEventWithExternal)
            );

            var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";
            var wrapperError = _fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex);

            Assert.Null(wrapperError);
        }
    }
}
