// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Errors
{
    [NetFrameworkTest]
    public class ErrorTraceWebService : IClassFixture<RemoteServiceFixtures.BasicWebService>
    {
        private const string ExpectedExceptionType = "System.Exception";


        private readonly RemoteServiceFixtures.BasicWebService _fixture;

        public ErrorTraceWebService(RemoteServiceFixtures.BasicWebService fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var newRelicConfig = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(newRelicConfig);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.DeleteXmlNodeFromNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreErrors");
                    CommonUtils.DeleteXmlNodeFromNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreStatusCodes");

                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreErrors", "");
                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreStatusCodes", "");

                },
                exerciseApplication: () =>
                {
                    _fixture.ThrowExceptionHttp();
                    _fixture.ThrowExceptionSoap();
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
				new Assertions.ExpectedMetric {metricName = @"Errors/all", callCount = 2},
                new Assertions.ExpectedMetric {metricName = @"Errors/allWeb", callCount = 2},
                new Assertions.ExpectedMetric {metricName = @"Errors/WebTransaction/WebService/BasicWebService.TestWebService.ThrowException", callCount = 2},

				// other
				new Assertions.ExpectedMetric {metricName = @"WebTransaction/WebService/BasicWebService.TestWebService.ThrowException", callCount = 2},
                new Assertions.ExpectedMetric {metricName = @"DotNet/BasicWebService.TestWebService.ThrowException", callCount = 2},
                new Assertions.ExpectedMetric {metricName = @"DotNet/BasicWebService.TestWebService.ThrowException", metricScope = "WebTransaction/WebService/BasicWebService.TestWebService.ThrowException", callCount = 2}
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"External/all" },
                new Assertions.ExpectedMetric { metricName = @"ApdexOther" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },
            };

            var expectedAttributes = new Dictionary<string, string>
            {
                { "errorType", ExpectedExceptionType },
                { "errorMessage", "Oh no!" },
            };

            var expectedErrorEventAttributes = new Dictionary<string, string>
            {
                { "error.class", ExpectedExceptionType },
                { "error.message", "Oh no!" },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();
            var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assert.True(errorTraces.Any(), "No error trace found."),
                () => Assert.True(errorTraces.Count == 2, $"Expected 2 errors traces but found {errorTraces.Count}"),
                () => Assert.Equal("WebTransaction/WebService/BasicWebService.TestWebService.ThrowException", errorTraces[0].Path),
                () => Assert.Equal(ExpectedExceptionType, errorTraces[0].ExceptionClassName),
                () => Assert.Equal("Oh no!", errorTraces[0].Message),
                () => Assert.NotEmpty(errorTraces[0].Attributes.StackTrace),
                () => Assert.Equal("WebTransaction/WebService/BasicWebService.TestWebService.ThrowException", errorTraces[1].Path),
                () => Assert.Equal(ExpectedExceptionType, errorTraces[1].ExceptionClassName),
                () => Assert.Equal("Oh no!", errorTraces[1].Message),
                () => Assert.NotEmpty(errorTraces[1].Attributes.StackTrace),
                () => Assert.True(transactionEvents.Any(), "No transaction events found."),
                () => Assert.True(transactionEvents.Count == 2, $"Expected 2 transaction event but found {transactionEvents.Count}"),
                () => Assertions.TransactionEventHasAttributes(expectedAttributes, TransactionEventAttributeType.Intrinsic, transactionEvents[0]),
                () => Assertions.TransactionEventHasAttributes(expectedAttributes, TransactionEventAttributeType.Intrinsic, transactionEvents[1]),
                () => Assert.Single(errorEvents),
                () => Assertions.ErrorEventHasAttributes(expectedErrorEventAttributes, EventAttributeType.Intrinsic, errorEvents[0].Events[0])
            );
        }
    }
}
