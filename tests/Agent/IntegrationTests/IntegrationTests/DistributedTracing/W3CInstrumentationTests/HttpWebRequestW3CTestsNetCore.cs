// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing.W3CInstrumentationTests
{
    /// <summary>
    /// Test W3C support when chaining multiple requests by using WebRequest.
    /// Instrumentations occur in this test are AspNetCore and HttpClient.
    /// </summary>
    [NetCoreTest]
    public class HttpWebRequestW3CTestsNetCore : NewRelicIntegrationTest<AspNetCoreDistTraceRequestChainFixture>
    {
        private readonly AspNetCoreDistTraceRequestChainFixture _fixture;

        private const int ExpectedTransactionCount = 1;
        private const string TestTraceId = "12345678901234567890123456789012";
        private const string TestTraceParent = "1234567890123456";
        private const string TestTracingVendors = "rojo,congo";
        private const string TestOtherVendorEntries = "rojo=1,congo=2";

        public HttpWebRequestW3CTestsNetCore(AspNetCoreDistTraceRequestChainFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    var headers = new KeyValuePair<string, string>[]
                    {
                        new KeyValuePair<string, string> ("traceparent", $"00-{TestTraceId}-{TestTraceParent}-00"),
                        new KeyValuePair<string, string> ("tracestate", TestOtherVendorEntries)
                    };

                    _fixture.ExecuteTraceRequestChain("WebRequestCallNext", "WebRequestCallNext", "CallEnd", headers);

                    _fixture.FirstCallAppAgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(15), ExpectedTransactionCount);
                    _fixture.SecondCallAppAgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(15), ExpectedTransactionCount);
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(15), ExpectedTransactionCount);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var senderAppTxEvent = _fixture.FirstCallAppAgentLog.GetTransactionEvents().FirstOrDefault();
            Assert.NotNull(senderAppTxEvent);

            var receiverAppTxEvents = _fixture.SecondCallAppAgentLog.GetTransactionEvents().FirstOrDefault();
            Assert.NotNull(receiverAppTxEvents);

            var senderAppSpanEvents = _fixture.FirstCallAppAgentLog.GetSpanEvents();
            var receiverAppSpanEvents = _fixture.SecondCallAppAgentLog.GetSpanEvents();

            Assert.Equal(senderAppTxEvent.IntrinsicAttributes["guid"], receiverAppTxEvents.IntrinsicAttributes["parentId"]);

            foreach (var span in senderAppSpanEvents)
            {
                Assert.Equal(TestTraceId, span.IntrinsicAttributes["traceId"]);
            }

            foreach (var span in receiverAppSpanEvents)
            {
                Assert.Equal(TestTraceId, span.IntrinsicAttributes["traceId"]);
            }

            var senderRootSpanEvent = senderAppSpanEvents.Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == "WebTransaction/MVC/FirstCall/WebRequestCallNext/{nextUrl}").FirstOrDefault();
            var externalSpanEvent = senderAppSpanEvents.Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == $"External/{RemoteApplication.DestinationServerName}/Stream/GET").FirstOrDefault();

            var receiverRootSpanEvent = receiverAppSpanEvents.Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == "WebTransaction/MVC/SecondCall/WebRequestCallNext/{nextUrl}").FirstOrDefault();

            Assert.NotNull(senderRootSpanEvent);
            Assert.Equal(TestTracingVendors, senderRootSpanEvent.IntrinsicAttributes["tracingVendors"]);
            Assert.Equal(TestTraceParent, senderRootSpanEvent.IntrinsicAttributes["parentId"]);
            Assert.False(senderRootSpanEvent.IntrinsicAttributes.ContainsKey("trustedParentId"));

            Assert.NotNull(receiverRootSpanEvent);
            Assert.Equal(TestTracingVendors, receiverRootSpanEvent.IntrinsicAttributes["tracingVendors"]);
            Assert.Equal(externalSpanEvent.IntrinsicAttributes["guid"], receiverRootSpanEvent.IntrinsicAttributes["parentId"]);
            Assert.Equal(externalSpanEvent.IntrinsicAttributes["guid"], receiverAppTxEvents.IntrinsicAttributes["parentSpanId"]);
            Assert.Equal(externalSpanEvent.IntrinsicAttributes["guid"], receiverRootSpanEvent.IntrinsicAttributes["trustedParentId"]);
            Assert.True(AttributeComparer.IsEqualTo(senderAppTxEvent.IntrinsicAttributes["priority"], receiverRootSpanEvent.IntrinsicAttributes["priority"]));

            var senderExpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/CreatePayload/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Accept/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Create/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/TraceState/NoNrEntry", callCount = 1 }
            };

            var accountId = _fixture.SecondCallAppAgentLog.GetAccountId();
            var appId = _fixture.SecondCallAppAgentLog.GetApplicationId();

            var receiverExpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Accept/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Create/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"DurationByCaller/App/{accountId}/{appId}/HTTP/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"DurationByCaller/App/{accountId}/{appId}/HTTP/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"TransportDuration/App/{accountId}/{appId}/HTTP/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"TransportDuration/App/{accountId}/{appId}/HTTP/allWeb", callCount = 1 }
            };

            var receiverUnexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/TraceState/NoNrEntry", callCount = 1 }
            };

            var senderActualMetrics = _fixture.FirstCallAppAgentLog.GetMetrics();
            var receiverActualMetrics = _fixture.SecondCallAppAgentLog.GetMetrics();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(senderExpectedMetrics, senderActualMetrics),
                () => Assertions.MetricsExist(receiverExpectedMetrics, receiverActualMetrics),
                () => Assertions.MetricsDoNotExist(receiverUnexpectedMetrics, receiverActualMetrics)
            );
        }
    }
}
