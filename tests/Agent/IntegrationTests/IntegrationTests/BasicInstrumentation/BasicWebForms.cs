// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class BasicWebForms : NewRelicIntegrationTest<RemoteServiceFixtures.BasicWebFormsApplication>
    {

        private readonly RemoteServiceFixtures.BasicWebFormsApplication _fixture;

        public BasicWebForms(RemoteServiceFixtures.BasicWebFormsApplication fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.GetSlow();
                    _fixture.Get404();

                    // Make a request with an invalid query string to ensure that the agent handles it safely
                    var queryStringParams = new Dictionary<string, string> { { "a", "<b>" } };
                    _fixture.GetWithQueryString(queryStringParams, true);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 3 }
            };

            foreach (var webFormName in new List<string>() { "webform1.aspx", "webformslow.aspx"} )
            {
                var endpointMetrics = new List<Assertions.ExpectedMetric>
                {
                new Assertions.ExpectedMetric { metricName = $"WebTransaction/ASP/{webFormName}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"WebTransactionTotalTime/ASP/{webFormName}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthenticateRequest", metricScope = $"WebTransaction/ASP/{webFormName}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthorizeRequest", metricScope = $"WebTransaction/ASP/{webFormName}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ResolveRequestCache", metricScope = $"WebTransaction/ASP/{webFormName}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/MapRequestHandler", metricScope = $"WebTransaction/ASP/{webFormName}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AcquireRequestState", metricScope = $"WebTransaction/ASP/{webFormName}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ExecuteRequestHandler", metricScope = $"WebTransaction/ASP/{webFormName}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/EndRequest", metricScope = $"WebTransaction/ASP/{webFormName}", callCount = 1 },
                };
                expectedMetrics.AddRange(endpointMetrics);
                if (webFormName == "webformslow.aspx")
                {
                    // These metrics don't appear when `webform1.aspx` is the requested endpoint because of the invalid query string
                    expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/ReleaseRequestState", metricScope = $"WebTransaction/ASP/{webFormName}", callCount = 1 });
                    expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/UpdateRequestCache", metricScope = $"WebTransaction/ASP/{webFormName}", callCount = 1 });
                }
            }
            expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"WebTransaction/StatusCode/404" });

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Integrated Pipeline" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Default/Ignored" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Normalized/*" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },
                new Assertions.ExpectedMetric { metricName = @"Supportability/Transactions/allOther" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Uri/WebFormThatDoesNotExist.aspx" }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                @"AuthenticateRequest",
                @"AuthorizeRequest",
                @"ResolveRequestCache",
                @"MapRequestHandler",
                @"AcquireRequestState",
                @"ExecuteRequestHandler",
                @"ReleaseRequestState",
                @"UpdateRequestCache",
                @"EndRequest",
            };
            var expectedTransactionTraceAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200" },
                { "http.statusCode", 200 }
            };
            var expectedTransactionEventIntrinsicAttributes1 = new Dictionary<string, string>
            {
                {"type", "Transaction"},
                {"nr.apdexPerfZone", "F"}
            };
            var expectedTransactionEventIntrinsicAttributes2 = new List<string>
            {
                "timestamp",
                "duration",
                "webDuration",
                "queueDuration",
                "totalTime"
            };
            var expectedTransactionEventAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200"},
                { "http.statusCode", 200 }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/ASP/webformslow.aspx")
                .FirstOrDefault();

            //order transactions chronologically
            var selectedTransactionEvent = _fixture.AgentLog.GetTransactionEvents()
                .Where(transactionEvent => transactionEvent != null
                    && transactionEvent.IntrinsicAttributes != null
                    && transactionEvent.IntrinsicAttributes.ContainsKey("timestamp"))
                .OrderBy(transactionEvent => transactionEvent.IntrinsicAttributes["timestamp"])
                .FirstOrDefault();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(selectedTransactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, selectedTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, selectedTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, selectedTransactionEvent)
            );
        }
    }
}
