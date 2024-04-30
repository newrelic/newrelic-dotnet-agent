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
    public class HighSecurityModeDisabled : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private const string QueryStringParameterValue = @"my thing";


        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public HighSecurityModeDisabled(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
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

                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("debug");
                    configModifier.SetHighSecurityMode(false);
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
            var expectedTransactionTraceAttributes = new Dictionary<string, string>
            {
                { "request.parameters.data", QueryStringParameterValue },
                { "host.displayName", "custom-host-name"}
            };

            var expectedTransactionEventAgentAttributes = new Dictionary<string, object>
            {
                { "response.status", "200"},
                { "http.statusCode", 200 },
                { "host.displayName", "custom-host-name"}
            };

            var expectedAgentErrorTraceAttributes = new Dictionary<string, string>
            {
                { "host.displayName", "custom-host-name"}
            };

            var expectedAgentErrorEventAttributes = new Dictionary<string, string>
            {
                { "host.displayName", "custom-host-name"}
            };

            var displayHost = _fixture.AgentLog.GetConnectData().DisplayHost;
            var getDataTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/Query");
            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();
            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();
            var firstErrorEvent = errorEvents.FirstOrDefault();
            var firstErrorTrace = errorTraces.FirstOrDefault();

            NrAssert.Multiple(
                () => Assert.Equal("custom-host-name", displayHost),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, getDataTransactionEvent),
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAttributes, TransactionTraceAttributeType.Agent, transactionSample),
                () => Assert.NotNull(firstErrorEvent),
                () => Assertions.ErrorEventHasAttributes(expectedAgentErrorEventAttributes, EventAttributeType.Agent, firstErrorEvent),
                () => Assertions.ErrorTraceHasAttributes(expectedAgentErrorTraceAttributes, ErrorTraceAttributeType.Agent, firstErrorTrace)


            );
        }
    }
}
