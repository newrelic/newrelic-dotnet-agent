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
    public class SecurityPoliciesMostRestrictiveTests : NewRelicIntegrationTest<RemoteServiceFixtures.SecurityPoliciesBasicMvcApplicationTestFixture>
    {
        private const string QueryStringParameterValue = @"my thing";
        private const string StripExceptionMessagesMessage = "Message removed by New Relic based on your currently enabled security settings.";

        private readonly RemoteServiceFixtures.SecurityPoliciesBasicMvcApplicationTestFixture _fixture;

        public SecurityPoliciesMostRestrictiveTests(RemoteServiceFixtures.SecurityPoliciesBasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    configModifier.AddAttributesInclude("request.parameters.*");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
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
                { "http.statusCode", 200 }
            };
            var expectedTransactionEventIntrinsicAttributes1 = new Dictionary<string, string>
            {
                {"type", "Transaction"}
                
            };
            var expectedTransactionEventIntrinsicAttributes2 = new List<string>
            {
                "nr.apdexPerfZone",
                "timestamp",
                "duration",
                "webDuration",
                "queueDuration",
                "totalTime",
                "name"
            };
            var expectedTransactionEventAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200"},
                { "http.statusCode", 200 }
            };

            var expectedErrorTransactionEventAttributes = new List<string>
            {
                "errorType",
                "errorMessage"
            };

            var expectedErrorEventAttributes = new Dictionary<string, string>
            {
                { "error.message", StripExceptionMessagesMessage}
            };

            const string originalErrorMessage = "!Exception~Message!";

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            var getDataTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/Query");
            var getExceptionTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/ThrowException");

            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();
            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();

            var firstErrorEvent = errorEvents.FirstOrDefault();
            var firstErrorTrace = errorTraces.FirstOrDefault();

            var stackTrace = firstErrorTrace?.Attributes.StackTrace.ToList();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(getDataTransactionEvent),
                () => Assert.NotNull(getExceptionTransactionEvent),
                () => Assert.NotNull(firstErrorEvent),
                () => Assert.NotNull(firstErrorTrace)
            );

            NrAssert.Multiple(
                () => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
                () => Assertions.ErrorEventHasAttributes(expectedErrorEventAttributes, EventAttributeType.Intrinsic, firstErrorEvent),
                () => Assert.Equal(StripExceptionMessagesMessage, firstErrorTrace.Message),
                () => Assert.Contains(StripExceptionMessagesMessage, stackTrace[0]),
                () => Assert.DoesNotContain(originalErrorMessage, stackTrace[0])
            );

            NrAssert.Multiple
            (
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),

                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, getDataTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, getDataTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, getDataTransactionEvent),


                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedErrorTransactionEventAttributes, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent)
            );
        }
    }
}
