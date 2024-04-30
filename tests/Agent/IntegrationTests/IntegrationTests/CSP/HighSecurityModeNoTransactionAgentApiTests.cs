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
    public class HighSecurityModeNoTransactionAgentApiTests : NewRelicIntegrationTest<RemoteServiceFixtures.HSMAgentApiExecutor>
    {

        private readonly RemoteServiceFixtures.HSMAgentApiExecutor _fixture;
        private const string StripExceptionMessagesMessage = "Message removed by New Relic based on your currently enabled security settings.";

        public HighSecurityModeNoTransactionAgentApiTests(RemoteServiceFixtures.HSMAgentApiExecutor fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = _fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.SetHighSecurityMode(true);
                    configModifier.SetLogLevel("debug");
                    configModifier.SetAutoStart(false);
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new[]
            {
                new Assertions.ExpectedMetric{ metricName = "Custom/MyMetric", callCount = 1}
            };

            var expectedErrorEventIntrinsicAttributes = new Dictionary<string, string>
            {
                { "error.class", "System.Exception" },
                { "type", "TransactionError" },
                { "error.message", StripExceptionMessagesMessage }
            };

            var unexpectedErrorEventCustomAttributes = new List<string>
            {
                "hey",
                "faz"
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            var errorTrace = _fixture.AgentLog.GetErrorTraces().FirstOrDefault();
            var errorEventPayload = _fixture.AgentLog.GetErrorEventPayloads().FirstOrDefault();

            NrAssert.Multiple
                (
                    () => Assertions.MetricsExist(expectedMetrics, actualMetrics),
                    () => Assert.NotNull(errorTrace),
                    () => Assert.Equal("2", errorEventPayload.Additions.EventsSeen.ToString()),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEventPayload.Events[0]),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEventPayload.Events[1]),
                    () => Assertions.ErrorEventDoesNotHaveAttributes(unexpectedErrorEventCustomAttributes, EventAttributeType.User, errorEventPayload.Events[1])
                );
        }
    }
}
