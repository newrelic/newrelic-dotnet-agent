// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.HttpClientInstrumentation.NetFramework
{
    [NetFrameworkTest]
    public class HttpClientInstrumentationNetFramework : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public HttpClientInstrumentationNetFramework(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
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
                new Assertions.ExpectedMetric { metricName = @"External/www.newrelic.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/docs.newrelic.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.newrelic.org/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.newrelic.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/docs.newrelic.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.newrelic.org/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.newrelic.com/Stream/GET", metricScope = @"WebTransaction/MVC/DefaultController/HttpClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/docs.newrelic.com/Stream/GET", metricScope = @"WebTransaction/MVC/DefaultController/HttpClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.newrelic.org/Stream/GET", metricScope = @"WebTransaction/MVC/DefaultController/HttpClientTaskCancelled", callCount = 1 }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"External/www.newrelic.com/Stream/GET",
                @"External/docs.newrelic.com/Stream/GET"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/MVC/DefaultController/HttpClient")
                .FirstOrDefault();

            Assert.NotNull(transactionSample);

            var transactionEventWithExternal = _fixture.AgentLog.GetTransactionEvents()
                .Where(e => e.IntrinsicAttributes.ContainsKey("externalDuration"))
                .FirstOrDefault();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assert.NotNull(transactionEventWithExternal)
            );

            var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";
            var wrapperError = _fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex);

            Assert.Null(wrapperError);
        }
    }
}
