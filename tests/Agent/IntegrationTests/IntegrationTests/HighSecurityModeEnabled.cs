/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class HighSecurityModeEnabled : IClassFixture<RemoteServiceFixtures.HSMBasicMvcApplicationTestFixture>
    {
        private const string QueryStringParameterValue = @"my thing";

        private readonly RemoteServiceFixtures.HSMBasicMvcApplicationTestFixture _fixture;

        public HighSecurityModeEnabled(RemoteServiceFixtures.HSMBasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
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

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "requestParameters" }, "enabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "service" }, "ssl", "false");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetWithData(QueryStringParameterValue);
                    _fixture.ThrowException();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var unexpectedAgentAttributes = new List<string>
            {
                @"request.parameters.data",
            };

            var expectedTransactionTraceAgentAttributes = new Dictionary<string, string>
            {
                { "response.status", "200" }
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
                "totalTime",
                "name",
                "nr.apdexPerfZone"
            };
            var expectedTransactionEventAgentAttributes = new Dictionary<string, string>
            {
                { "response.status", "200"}
            };


            var unexpectedErrorTraceMessage = "ExceptionMessage";

            var unexpectedErrorTransactionEventAttributes = new List<string>
            {
                "errorMessage"
            };

            var expectedErrorTransactionEventAttributes = new List<string>
            {
                "errorType"
            };

            var unexpectedErrorEventAttributes = new List<string>
            {
                "error.message"
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            var getDataTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/Query");
            var getExceptionTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/ThrowException");

            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();
            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();

            var firstErrorEvent = errorEvents.FirstOrDefault()?.Events.FirstOrDefault();
            var firstErrorTrace = errorTraces.FirstOrDefault();

            var stackTrace = firstErrorTrace.Attributes.StackTrace.ToList();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(getDataTransactionEvent),
                () => Assert.NotNull(getExceptionTransactionEvent),
                () => Assert.NotNull(firstErrorEvent),
                () => Assert.NotNull(firstErrorTrace)
            );

            NrAssert.Multiple(
                () => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
                () => Assertions.TransactionEventDoesNotHaveAttributes(unexpectedErrorTransactionEventAttributes, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent),
                () => Assertions.ErrorEventDoesNotHaveAttributes(unexpectedErrorEventAttributes, EventAttributeType.Intrinsic, firstErrorEvent),
                () => Assert.Empty(firstErrorTrace.Message),
                () => Assert.DoesNotContain(unexpectedErrorTraceMessage, stackTrace[0])
            );

            NrAssert.Multiple
            (
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, getDataTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, getDataTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, getDataTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedErrorTransactionEventAttributes, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent)
            );
        }
    }
}
