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
    public class AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTests : IClassFixture<RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture>
    {
        private const String QueryStringParameterValue = @"my thing";

        [NotNull]
        private readonly RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture _fixture;

        public AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTests([NotNull] RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture fixture, [NotNull] ITestOutputHelper output)
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
            var unexpectedAgentAttributes = new List<String>
            {
                @"request.parameters.data",
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
                "totalTime",
                "name"
            };
            var expectedTransactionEventAgentAttributes = new Dictionary<String, String>
            {
                { "response.status", "200"}
            };

            var unexpectedErrorTransactionEventAttributes = new List<String>
            {
                "errorMessage"
            };

            var expectedErrorTransactionEventAttributes = new List<String>
            {
                "errorType"
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            var getDataTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/Home/Query/{data}");
            var getExceptionTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/Home/ThrowException");

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(getDataTransactionEvent),
                () => Assert.NotNull(getExceptionTransactionEvent)
            );

            NrAssert.Multiple(
                () => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
                () => Assertions.TransactionEventDoesNotHaveAttributes(unexpectedErrorTransactionEventAttributes, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent)
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
