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
    public class HttpClientInstrumentationNet5 : NewRelicIntegrationTest<AspNet5BasicWebApiApplicationFixture>
    {
        private readonly AspNet5BasicWebApiApplicationFixture _fixture;

        public HttpClientInstrumentationNet5(AspNet5BasicWebApiApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
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
                    _fixture.MakeExternalCallUsingHttpClient("http://www.google.com", "/search");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"External/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET", metricScope = @"WebTransaction/MVC/Default/MakeExternalCallUsingHttpClient/{baseAddress}/{path}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/SpanEvent/TotalEventsSeen", CallCountAllHarvests = 4 }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"External/www.google.com/Stream/GET",
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/MVC/Default/MakeExternalCallUsingHttpClient/{baseAddress}/{path}")
                .FirstOrDefault();

            Assert.NotNull(transactionSample);

            var externalSpanEvents = _fixture.AgentLog.GetSpanEvents().Where(e => e.IntrinsicAttributes.TryGetValue("category", out var value) && value.Equals("http")).ToList();
            Assert.True(externalSpanEvents.Count() == 1);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assert.True(externalSpanEvents[0].IntrinsicAttributes.TryGetValue("component", out var value) && value.Equals("System.Net.Http.SocketsHttpHandler"))
            );
        }
    }
}
