// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
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
    public class ErrorTraceWebApi : NewRelicIntegrationTest<RemoteServiceFixtures.OwinWebApiFixture>
    {
        private readonly RemoteServiceFixtures.OwinWebApiFixture _fixture;

        public ErrorTraceWebApi(RemoteServiceFixtures.OwinWebApiFixture fixture, ITestOutputHelper testLogger) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = testLogger;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var newRelicConfig = fixture.DestinationNewRelicConfigFilePath;

                    var configModifier = new NewRelicConfigModifier(newRelicConfig);

                    CommonUtils.DeleteXmlNodeFromNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreErrors");
                    CommonUtils.DeleteXmlNodeFromNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreStatusCodes");

                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreErrors", "");
                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreStatusCodes", "");
                },
                exerciseApplication: () =>
                {
                    _fixture.ThrowException();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.ErrorTraceDataLogLineRegex, TimeSpan.FromMinutes(2));
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
                new Assertions.ExpectedMetric {metricName = @"Errors/WebTransaction/WebAPI/Values/ThrowException", callCount = 1 },

				// other
				new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 }
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all", callCount = 5 },
            };

            var expectedErrorClass = "System.Exception";
            var expectedErrorMessage = "ExceptionMessage";

            var expectedAttributes = new Dictionary<string, string>
            {
                { "errorType", expectedErrorClass },
                { "errorMessage", expectedErrorMessage },
            };


            var expectedErrorEventAttributes = new Dictionary<string, string>
            {
                { "error.class", expectedErrorClass },
                { "error.message", expectedErrorMessage },
            };

            var expectedErrorTransactionEventAttributes2 = new List<string> { "error" };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var errorTrace = _fixture.AgentLog.GetErrorTraces().ToList().FirstOrDefault();
            var transactionEvent = _fixture.AgentLog.GetTransactionEvents().ToList().FirstOrDefault();
            var errorEvent = _fixture.AgentLog.GetErrorEvents().FirstOrDefault();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assert.True(errorTrace != null, "No error trace found."),
                () => Assert.True(transactionEvent != null, "No transaction event found."),
                () => Assert.True(errorEvent != null, "No error event found."),
                () => Assert.Equal("WebTransaction/WebAPI/Values/ThrowException", errorTrace.Path),
                () => Assert.Equal(expectedErrorClass, errorTrace.ExceptionClassName),
                () => Assert.Equal(expectedErrorMessage, errorTrace.Message),
                () => Assert.NotEmpty(errorTrace.Attributes.StackTrace),
                () => Assertions.TransactionEventHasAttributes(expectedAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.ErrorEventHasAttributes(expectedErrorEventAttributes, EventAttributeType.Intrinsic, errorEvent),
                () => Assertions.TransactionEventHasAttributes(expectedErrorTransactionEventAttributes2, TransactionEventAttributeType.Intrinsic, transactionEvent)
            );
        }
    }
}
