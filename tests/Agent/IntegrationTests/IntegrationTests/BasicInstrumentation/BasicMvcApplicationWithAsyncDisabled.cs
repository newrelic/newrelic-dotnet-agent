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
    public class BasicMvcApplicationWithAsyncDisabled : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {

        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public BasicMvcApplicationWithAsyncDisabled(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
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
                    _fixture.GetWithAsyncDisabled();
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthenticateRequest", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthorizeRequest", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ResolveRequestCache", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/MapRequestHandler", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AcquireRequestState", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ExecuteRequestHandler", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DisableAsyncSupportController/Index", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ReleaseRequestState", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/UpdateRequestCache", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/EndRequest", metricScope = @"WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Integrated Pipeline" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Default/Ignored" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Normalized/*" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },

				// The .NET agent does not have the information needed to generate this metric
				new Assertions.ExpectedMetric { metricName = @"CPU/WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"CPU/WebTransaction/MVC/DisableAsyncSupportController/Index", callCount = 1 },
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"AuthenticateRequest",
                @"AuthorizeRequest",
                @"ResolveRequestCache",
                @"MapRequestHandler",
                @"AcquireRequestState",
                @"ExecuteRequestHandler",
                @"DotNet/DisableAsyncSupportController/Index",
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
                .Where(sample => sample.Path == @"WebTransaction/MVC/DisableAsyncSupportController/Index")
                .FirstOrDefault();
            var transactionEvent = _fixture.AgentLog.GetTransactionEvents()
                .FirstOrDefault();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent),
                () => Assert.NotNull(_fixture.ResponseBody)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent),
                () => JavaScriptAgent.GetJavaScriptAgentConfigFromSource(_fixture.ResponseBody),
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );
        }
    }
}
