// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetCoreTest]
    public class BasicAspNetCoreRazorTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicAspNetCoreRazorApplicationFixture>
    {
        private readonly RemoteServiceFixtures.BasicAspNetCoreRazorApplicationFixture _fixture;
        private string _responseBody;

        public BasicAspNetCoreRazorTests(RemoteServiceFixtures.BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
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
                    configModifier.SetCodeLevelMetricsEnabled();
                    configModifier.EnableAspNetCore6PlusBrowserInjection(true);
                },
                exerciseApplication: () =>
                {
                    _responseBody = _fixture.Get("Index");
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Razor/Pages/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/Razor/Pages/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Pages_Index/OnGet", metricScope = @"WebTransaction/Razor/Pages/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/Razor/Pages/Index", callCount = 1 },
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Normalized/*" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },

                // The .NET agent does not have the information needed to generate this metric
                new Assertions.ExpectedMetric { metricName = @"CPU/WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"CPU/WebTransaction/Razor/Pages/Index", callCount = 1 },
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"DotNet/Pages_Index/OnGet",
            };
            var expectedTransactionTraceAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200" },
                { "http.statusCode", 200 },
                { "request.uri", "/Index" }
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
                "totalTime"
            };
            var expectedTransactionEventAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200" },
                { "http.statusCode", 200 },
                { "request.uri", "/Index" }
            };

            var expectedGetIndexAttributes = new Dictionary<string, string>()
            {
                { "code.namespace", "BasicAspNetCoreRazorApplication.Pages.Pages_Index" },
                { "code.function", "OnGet" }
            };

            var connect = _fixture.AgentLog.GetConnectData().Environment.GetPluginList();
            Assert.DoesNotContain(connect, x => x.Contains("NewRelic.Providers.Wrapper.AspNetCore6Plus"));

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSamples = _fixture.AgentLog.GetTransactionSamples().ToList();

            var transactionSample = transactionSamples
                .FirstOrDefault(sample => sample.Path == @"WebTransaction/Razor/Pages/Index");
            var transactionEvent = _fixture.AgentLog.GetTransactionEvents()
                .FirstOrDefault();

            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();
            var getIndexSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "DotNet/Pages_Index/OnGet");

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent),
                () => Assert.NotNull(_responseBody)
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
                () => Assertions.SpanEventHasAttributes(expectedGetIndexAttributes, SpanEventAttributeType.Agent, getIndexSpan),
                () => JavaScriptAgent.GetJavaScriptAgentConfigFromSource(_responseBody),
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );
        }
    }
}
