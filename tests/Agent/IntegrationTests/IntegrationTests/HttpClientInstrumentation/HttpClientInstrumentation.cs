// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.HttpClientInstrumentation
{
    public abstract class HttpClientInstrumentationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        protected HttpClientInstrumentationTestsBase(TFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                        .ForceTransactionTraces()
                        .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    // We should observe 3 transactions finish their transform
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2), 3);
                }
            );

            _fixture.AddCommand("HttpClientDriver Get http://www.google.com");
            _fixture.AddCommand("HttpClientDriver CancelledGetOperation http://www.bing.com");
            _fixture.AddCommand("HttpClientDriver FactoryGet http://www.yahoo.com");

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"External/all", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"External/allOther", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET", metricScope = @"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.HttpClientDriver/Get", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"External/www.bing.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.bing.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.bing.com/Stream/GET", metricScope = @"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.HttpClientDriver/CancelledGetOperation", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/Stream/GET", metricScope = @"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.HttpClientDriver/FactoryGet", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"Supportability/SpanEvent/TotalEventsSeen", CallCountAllHarvests = 9 }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"External/www.google.com/Stream/GET",
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.HttpClientDriver/Get")
                .FirstOrDefault();

            Assert.NotNull(transactionSample);

            var transactionEventWithExternal = _fixture.AgentLog
                .GetTransactionEvents()
                .FirstOrDefault(e => e.IntrinsicAttributes.ContainsKey("externalDuration"));

            Assert.NotNull(transactionEventWithExternal);

            var externalSpanEvents = _fixture.AgentLog.GetSpanEvents().Where(e => e.IntrinsicAttributes.TryGetValue("category", out var value) && value.Equals("http")).ToList();
            Assert.True(externalSpanEvents.Count() == 3);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assert.True(externalSpanEvents[0].IntrinsicAttributes.TryGetValue("component", out var value) && value.ToString().StartsWith("System.Net.Http.")),
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

    [NetCoreTest]
    public class HttpClientInstrumentationTests_Net31 : HttpClientInstrumentationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public HttpClientInstrumentationTests_Net31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class HttpClientInstrumentationTests_Net50 : HttpClientInstrumentationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public HttpClientInstrumentationTests_Net50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class HttpClientInstrumentationTests_NetCoreLatest : HttpClientInstrumentationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public HttpClientInstrumentationTests_NetCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class HttpClientInstrumentationTests_FW462 : HttpClientInstrumentationTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public HttpClientInstrumentationTests_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class HttpClientInstrumentationTests_FW471 : HttpClientInstrumentationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public HttpClientInstrumentationTests_FW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class HttpClientInstrumentationTests_FW48 : HttpClientInstrumentationTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public HttpClientInstrumentationTests_FW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
