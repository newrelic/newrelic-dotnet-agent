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
    public class BasicOpenRastaApplication : NewRelicIntegrationTest<RemoteServiceFixtures.BasicOpenRastaApplication>
    {

        private readonly RemoteServiceFixtures.BasicOpenRastaApplication _fixture;

        public BasicOpenRastaApplication(RemoteServiceFixtures.BasicOpenRastaApplication fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.GetWithQueryString();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthenticateRequest", metricScope = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthorizeRequest", metricScope = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ResolveRequestCache", metricScope = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/MapRequestHandler", metricScope = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AcquireRequestState", metricScope = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ExecuteRequestHandler", metricScope = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ReleaseRequestState", metricScope = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/UpdateRequestCache", metricScope = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/EndRequest", metricScope = @"WebTransaction/OpenRasta/home/get", callCount = 1 },
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Integrated Pipeline" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Default/Ignored" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Normalized/*" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },
                new Assertions.ExpectedMetric { metricName = @"Supportability/Transactions/allOther" },
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"AuthenticateRequest",
                @"AuthorizeRequest",
                @"ResolveRequestCache",
                @"MapRequestHandler",
                @"AcquireRequestState",
                @"ExecuteRequestHandler",
                @"views/homeview.aspx",
                @"DotNet/home/get",
                @"ReleaseRequestState",
                @"UpdateRequestCache",
                @"EndRequest"
            };

            var expectedTransactionTraceAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200" },
                { "http.statusCode", 200 }
            };

            var expectedTransactionEventIntrinsicAttributes1 = new Dictionary<string, string>
            {
                {"type", "Transaction"}
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
                .Where(sample => sample.Path == @"WebTransaction/OpenRasta/home/get")
                .FirstOrDefault();
            var transactionEvent = _fixture.AgentLog.GetTransactionEvents()
                .FirstOrDefault();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent)
            );
        }
    }
}
