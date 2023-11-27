// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing.W3CInstrumentationTests
{
    /// <summary>
    /// Test W3C support when chaining multiple requests by using RestSharp.
    /// Instrumentations occur in this test are RestSharp and AspNet.
    /// </summary>
    [NetFrameworkTest]
    public class RestSharpW3CTests : W3CTestBase<RemoteServiceFixtures.FrameworkTracingChainFixture>
    {
        public RestSharpW3CTests(RemoteServiceFixtures.FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            fixture.Actions
            (
                exerciseApplication: () =>
                {
                    fixture.ExecuteTraceRequestChainRestSharp(Headers);

                    fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                    fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                    fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );
            fixture.Initialize();
        }

        [Fact]
        public override void RootSpanAttributes()
        {
            var senderRootSpanEvent = SenderAppSpanEvents.Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == "WebTransaction/MVC/DistributedTracingController/MakeExternalCallUsingRestClient").FirstOrDefault();
            var externalSpanEvent = SenderAppSpanEvents.Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == $"External/{RemoteApplication.DestinationServerName}/Stream/GET").FirstOrDefault();

            var receiverRootSpanEvent = ReceiverAppSpanEvents.Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == "WebTransaction/WebAPI/RestAPI/Get").FirstOrDefault();

            Assert.NotNull(senderRootSpanEvent);
            Assert.Equal(TestTracingVendors, senderRootSpanEvent.IntrinsicAttributes["tracingVendors"]);
            Assert.Equal(TestTraceParent, senderRootSpanEvent.IntrinsicAttributes["parentId"]);
            Assert.False(senderRootSpanEvent.IntrinsicAttributes.ContainsKey("trustedParentId"));

            Assert.NotNull(receiverRootSpanEvent);
            Assert.Equal(TestTracingVendors, receiverRootSpanEvent.IntrinsicAttributes["tracingVendors"]);
            Assert.Equal(externalSpanEvent.IntrinsicAttributes["guid"], receiverRootSpanEvent.IntrinsicAttributes["parentId"]);
            Assert.Equal(externalSpanEvent.IntrinsicAttributes["guid"], ReceiverAppTxEvent.IntrinsicAttributes["parentSpanId"]);
            Assert.Equal(externalSpanEvent.IntrinsicAttributes["guid"], receiverRootSpanEvent.IntrinsicAttributes["trustedParentId"]);
        }
    }
}
