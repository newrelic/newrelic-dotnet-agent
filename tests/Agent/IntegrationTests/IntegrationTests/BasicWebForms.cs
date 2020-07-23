using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class BasicWebForms : IClassFixture<RemoteServiceFixtures.BasicWebFormsApplication>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.BasicWebFormsApplication _fixture;

        public BasicWebForms([NotNull] RemoteServiceFixtures.BasicWebFormsApplication fixture, [NotNull] ITestOutputHelper output)
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
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/ASP/webformslow.aspx", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthenticateRequest", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthorizeRequest", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ResolveRequestCache", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/MapRequestHandler", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AcquireRequestState", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ExecuteRequestHandler", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },

				// On hold until we port the Page.PerformPreInit instrumentation to a wrapper
				//new Assertions.ExpectedMetric { metricName = @"DotNet/System.Web.UI.Page/PerformPreInit", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },

				new Assertions.ExpectedMetric { metricName = @"DotNet/ReleaseRequestState", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/UpdateRequestCache", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/EndRequest", metricScope = @"WebTransaction/ASP/webformslow.aspx", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"WebTransaction/StatusCode/404" }
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Integrated Pipeline" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Default/Ignored" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Normalized/*" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },
                new Assertions.ExpectedMetric { metricName = @"Supportability/Transactions/allOther" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Uri/WebFormThatDoesNotExist.aspx" }
            };
            var expectedTransactionTraceSegments = new List<String>
            {
                @"AuthenticateRequest",
                @"AuthorizeRequest",
                @"ResolveRequestCache",
                @"MapRequestHandler",
                @"AcquireRequestState",
                @"ExecuteRequestHandler",
				
				// On hold until we port the Page.PerformPreInit instrumentation to a wrapper
				//@"DotNet/System.Web.UI.Page/PerformPreInit",

				@"ReleaseRequestState",
                @"UpdateRequestCache",
                @"EndRequest",
            };
            var expectedTransactionTraceAgentAttributes = new Dictionary<String, String>
            {
                { "response.status", "200" }
            };
            var expectedTransactionEventIntrinsicAttributes1 = new Dictionary<String, String>
            {
                {"type", "Transaction"},
                {"nr.apdexPerfZone", "F"}
            };
            var expectedTransactionEventIntrinsicAttributes2 = new List<String>
            {
                "timestamp",
                "duration",
                "webDuration",
                "queueDuration",
                "totalTime"
            };
            var expectedTransactionEventAgentAttributes = new Dictionary<String, String>
            {
                { "response.status", "200"}
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/ASP/webformslow.aspx")
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
