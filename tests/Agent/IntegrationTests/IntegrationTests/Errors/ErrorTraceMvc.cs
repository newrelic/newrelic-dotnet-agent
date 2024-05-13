// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Errors
{
    [NetFrameworkTest]
    public class ErrorTraceMvc : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public ErrorTraceMvc(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper testLogger) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = testLogger;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var newRelicConfig = fixture.DestinationNewRelicConfigFilePath;

                    var configModifier = new NewRelicConfigModifier(newRelicConfig);

                    CommonUtils.DeleteXmlNodeFromNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreClasses");
                    CommonUtils.DeleteXmlNodeFromNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreStatusCodes");

                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreStatusCodes", "");
                },
                exerciseApplication: () =>
                {
                    _fixture.ThrowException();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // error metrics
                new Assertions.ExpectedMetric {metricName = @"Errors/all", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Errors/allWeb", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Errors/WebTransaction/MVC/DefaultController/ThrowException", callCount = 1},

                // other
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthenticateRequest", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AuthorizeRequest", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ResolveRequestCache", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/MapRequestHandler", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/AcquireRequestState", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/ExecuteRequestHandler", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DefaultController/ThrowException", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/EndRequest", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // These metrics won't get generated because of the exception
                new Assertions.ExpectedMetric { metricName = @"DotNet/ReleaseRequestState", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/UpdateRequestCache", metricScope = @"WebTransaction/MVC/DefaultController/ThrowException", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Integrated Pipeline" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Default/Ignored" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Normalized/*" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },
                new Assertions.ExpectedMetric { metricName = @"Supportability/Transactions/allOther" },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();
            var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();

            var transactionId = transactionEvents[0].IntrinsicAttributes["guid"].ToString();

            var expectedTransactionEventAttributes = new Dictionary<string, string>
            {
                { "errorType", "System.Exception" },
                { "errorMessage", "!Exception~Message!" },
                { "error", "true" }
            };

            var expectedErrorEventAttributes = new Dictionary<string, string>
            {
                { "error.class", "System.Exception" },
                { "error.message", "!Exception~Message!" },
                { "guid", transactionId }
            };

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assert.True(errorTraces.Any(), "No error trace found."),
                () => Assert.True(errorTraces.Count == 1, $"Expected 1 errors traces but found {errorTraces.Count}"),
                () => Assert.Equal("WebTransaction/MVC/DefaultController/ThrowException", errorTraces[0].Path),
                () => Assert.Equal("System.Exception", errorTraces[0].ExceptionClassName),
                () => Assert.Equal("!Exception~Message!", errorTraces[0].Message),
                () => Assert.NotEmpty(errorTraces[0].Attributes.StackTrace),
                () => Assert.Equal(errorTraces[0].Attributes.IntrinsicAttributes["guid"], transactionId),
                () => Assert.True(transactionEvents.Any(), "No transaction events found."),
                () => Assert.True(transactionEvents.Count == 1, $"Expected 1 transaction event but found {transactionEvents.Count}"),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAttributes, TransactionEventAttributeType.Intrinsic, transactionEvents[0]),
                () => Assert.Single(errorEvents),
                () => Assertions.ErrorEventHasAttributes(expectedErrorEventAttributes, EventAttributeType.Intrinsic, errorEvents[0])
            );
        }
    }
}
