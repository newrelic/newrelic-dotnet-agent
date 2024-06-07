// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CSP
{
    [NetFrameworkTest]
    public class HighSecurityModeEnabled : NewRelicIntegrationTest<RemoteServiceFixtures.HSMBasicMvcApplicationTestFixture>
    {
        private const string QueryStringParameterValue = @"my thing";

        private readonly RemoteServiceFixtures.HSMBasicMvcApplicationTestFixture _fixture;

        private const string StripExceptionMessagesMessage = "Message removed by New Relic based on your currently enabled security settings.";
        public HighSecurityModeEnabled(RemoteServiceFixtures.HSMBasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    configModifier.SetLogLevel("debug");
                    configModifier.SetHighSecurityMode(true);
                    configModifier.AddAttributesInclude("request.parameters.*");
                    configModifier.SetTransactionTracerRecordSql("raw");
                    configModifier.SetCustomHostName("custom-host-name");
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

            var expectedTransactionTraceAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200" },
                { "http.statusCode", 200 },
                { "host.displayName", "custom-host-name"}
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
            var expectedTransactionEventAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200"},
                { "http.statusCode", 200 },
                { "host.displayName", "custom-host-name"}
            };

            var expectedErrorTransactionEventAttributes = new List<string>
            {
                "errorType",
                "errorMessage"
            };

            var expectedAgentErrorTraceAttributes = new Dictionary<string, string>
            {
                { "host.displayName", "custom-host-name"}
            };

            var expectedIntrinsicErrorEventAttributes = new Dictionary<string, string>
            {
                { "error.message", StripExceptionMessagesMessage},
            };

            var expectedAgentErrorEventAttributes = new Dictionary<string, string>
            {
                { "host.displayName", "custom-host-name"}
            };

            const string originalErrorMessage = "!Exception~Message!";

            var displayHost = _fixture.AgentLog.GetConnectData().DisplayHost;
            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            var getDataTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/Query");
            var getExceptionTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/ThrowException");

            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();
            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();

            var firstErrorEvent = errorEvents.FirstOrDefault();
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
                () => Assert.Equal(StripExceptionMessagesMessage, firstErrorTrace.Message),
                () => Assert.Contains(StripExceptionMessagesMessage, stackTrace[0]),
                () => Assert.DoesNotContain(originalErrorMessage, stackTrace[0]),
                () => Assert.Equal("custom-host-name", displayHost)
            );

            NrAssert.Multiple
            (
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, getDataTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, getDataTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, getDataTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedErrorTransactionEventAttributes, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent),
                () => Assertions.ErrorTraceHasAttributes(expectedAgentErrorTraceAttributes, ErrorTraceAttributeType.Agent, firstErrorTrace),
                () => Assertions.ErrorEventHasAttributes(expectedIntrinsicErrorEventAttributes, EventAttributeType.Intrinsic, firstErrorEvent),
                () => Assertions.ErrorEventHasAttributes(expectedAgentErrorEventAttributes, EventAttributeType.Agent, firstErrorEvent)
            );
        }
    }
}
