// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers;
using System.Collections.Generic;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.IntegrationTests.Owin
{
    [NetFrameworkTest]
    public class OwinDTChainTests : IClassFixture<OwinTracingChainFixture>
    {
        private readonly OwinTracingChainFixture _fixture;

        public OwinDTChainTests(OwinTracingChainFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetOrDeleteSpanEventsEnabled(true);
                    configModifier.SetLogLevel("all");

                    _fixture.ReceiverApplication = _fixture.SetupReceiverApplication(isDistributedTracing: true, isWebApplication: false);
                    _fixture.ReceiverApplication.Start(string.Empty, captureStandardOutput: true);
                },
                exerciseApplication: () =>
                {
                    _fixture.ExecuteTraceRequestChainHttpClient();

                    _fixture.ReceiverApplication.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                    _fixture.ReceiverApplication.AgentLog.WaitForLogLine(AgentLogFile.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var senderAppTxEvent = _fixture.AgentLog.GetTransactionEvents().FirstOrDefault();
            Assert.NotNull(senderAppTxEvent);

            var receiverAppTxEvent = _fixture.ReceiverApplication.AgentLog.GetTransactionEvents().FirstOrDefault();
            Assert.NotNull(receiverAppTxEvent);

            var senderAppSpanEvents = _fixture.AgentLog.GetSpanEvents();
            var receiverAppSpanEvents = _fixture.ReceiverApplication.AgentLog.GetSpanEvents();

            var externalSpanEvent = senderAppSpanEvents.Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == "External/localhost/Stream/GET")
                .FirstOrDefault();
            Assert.Equal(externalSpanEvent.IntrinsicAttributes["guid"], receiverAppTxEvent.IntrinsicAttributes["parentSpanId"]);

            var owinSpanEvent = senderAppSpanEvents.Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == "Owin Middleware Pipeline")
                .FirstOrDefault();
            Assert.NotNull(owinSpanEvent);

            Assert.Equal(senderAppTxEvent.IntrinsicAttributes["guid"], receiverAppTxEvent.IntrinsicAttributes["parentId"]);

            var distributedTraceId = senderAppTxEvent.IntrinsicAttributes["traceId"];

            foreach (var span in senderAppSpanEvents)
            {
                Assert.Equal(distributedTraceId, span.IntrinsicAttributes["traceId"]);
                Assert.Equal(senderAppTxEvent.IntrinsicAttributes["guid"], span.IntrinsicAttributes["transactionId"]);
            }

            foreach (var span in receiverAppSpanEvents)
            {
                Assert.Equal(distributedTraceId, span.IntrinsicAttributes["traceId"]);
                Assert.Equal(receiverAppTxEvent.IntrinsicAttributes["guid"], span.IntrinsicAttributes["transactionId"]);
            }

            var senderExpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/CreatePayload/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Create/Success", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"Supportability/SpanEvent/TotalEventsSeen", callCount = 4 },

                new Assertions.ExpectedMetric { metricName = @"DurationByCaller/Unknown/Unknown/Unknown/HTTP/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DurationByCaller/Unknown/Unknown/Unknown/HTTP/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"TransportDuration/Unknown/Unknown/Unknown/HTTP/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"TransportDuration/Unknown/Unknown/Unknown/HTTP/allWeb", callCount = 1 },
            };

            var acctId = _fixture.AgentLog.GetAccountId();
            var appId = _fixture.AgentLog.GetApplicationId();

            var receiverExpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Accept/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/SpanEvent/TotalEventsSeen", callCount = 3 },

                new Assertions.ExpectedMetric { metricName = $"DurationByCaller/App/{acctId}/{appId}/HTTP/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"DurationByCaller/App/{acctId}/{appId}/HTTP/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"TransportDuration/App/{acctId}/{appId}/HTTP/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"TransportDuration/App/{acctId}/{appId}/HTTP/allWeb", callCount = 1 },
            };

            var senderActualMetrics = _fixture.AgentLog.GetMetrics();
            var receiverActualMetrics = _fixture.ReceiverApplication.AgentLog.GetMetrics();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(senderExpectedMetrics, senderActualMetrics),
                () => Assertions.MetricsExist(receiverExpectedMetrics, receiverActualMetrics)
            );

            var transportDurationMetric = _fixture.ReceiverApplication.AgentLog.GetMetricByName($"TransportDuration/App/{acctId}/{appId}/HTTP/all");
            Assert.True(transportDurationMetric.Values.Total > 0);
        }
    }
}
