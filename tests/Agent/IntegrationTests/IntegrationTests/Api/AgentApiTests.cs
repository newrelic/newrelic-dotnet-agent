// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Api
{
    [NetFrameworkTest]
    public class AgentApiTests : NewRelicIntegrationTest<RemoteServiceFixtures.AgentApiExecutor>
    {

        private readonly RemoteServiceFixtures.AgentApiExecutor _fixture;

        public AgentApiTests(RemoteServiceFixtures.AgentApiExecutor fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);

                    CommonUtils.ModifyOrCreateXmlAttributesInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, new[] { new KeyValuePair<string, string>("autoStart", "false") });
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
                { "error.message", "Rawr!" },
                { "type", "TransactionError" }
            };

            var expectedErrorEventCustomAttributes = new Dictionary<string, string>
            {
                {"hey", "dude"},
                {"faz", "baz"}
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            var errorTrace = _fixture.AgentLog.GetErrorTraces().FirstOrDefault();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().OrderBy(ev => ev.IntrinsicAttributes["timestamp"]).ToList();

            NrAssert.Multiple
                (
                    () => Assertions.MetricsExist(expectedMetrics, actualMetrics),
                    () => Assert.NotNull(errorTrace),
                    () => Assert.Equal(2, errorEvents.Count),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEvents[0]),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEvents[1]),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventCustomAttributes, EventAttributeType.User, errorEvents[1])
                );
        }
    }
}
