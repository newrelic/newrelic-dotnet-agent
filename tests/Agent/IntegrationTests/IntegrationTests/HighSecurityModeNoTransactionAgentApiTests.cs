// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class HighSecurityModeNoTransactionAgentApiTests : IClassFixture<RemoteServiceFixtures.HSMAgentApiExecutor>
    {
        private readonly RemoteServiceFixtures.HSMAgentApiExecutor _fixture;

        public HighSecurityModeNoTransactionAgentApiTests(RemoteServiceFixtures.HSMAgentApiExecutor fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = _fixture.DestinationNewRelicConfigFilePath;

                    CommonUtils.ModifyOrCreateXmlAttributesInNewRelicConfig(configPath, new[] { "configuration", "service" }, new[] { new KeyValuePair<string, string>("autoStart", "false") });

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level",
                        "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" },
                        "enabled", "true");
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
                { "type", "TransactionError" }
            };


            var unexpectedErrorEventIntrinsicAttributes = new List<string> { "error.message" };

            var unexpectedErrorEventCustomAttributes = new List<string>
            {
                "hey",
                "faz"
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            var errorTrace = _fixture.AgentLog.GetErrorTraces().FirstOrDefault();
            var errorEventPayload = _fixture.AgentLog.GetErrorEvents().FirstOrDefault();

            NrAssert.Multiple
                (
                    () => Assertions.MetricsExist(expectedMetrics, actualMetrics),
                    () => Assert.NotNull(errorTrace),
                    () => Assert.Equal("100", errorEventPayload.Additions.ReservoirSize.ToString()),
                    () => Assert.Equal("2", errorEventPayload.Additions.EventsSeen.ToString()),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEventPayload.Events[0]),
                    () => Assertions.ErrorEventDoesNotHaveAttributes(unexpectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEventPayload.Events[0]),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEventPayload.Events[1]),
                    () => Assertions.ErrorEventDoesNotHaveAttributes(unexpectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEventPayload.Events[1]),
                    () => Assertions.ErrorEventDoesNotHaveAttributes(unexpectedErrorEventCustomAttributes, EventAttributeType.User, errorEventPayload.Events[1])
                );
        }
    }
}
