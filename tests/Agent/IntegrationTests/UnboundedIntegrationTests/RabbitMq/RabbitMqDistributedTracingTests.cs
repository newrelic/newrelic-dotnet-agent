// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;


namespace NewRelic.Agent.UnboundedIntegrationTests.RabbitMq
{
    [NetFrameworkTest]
    public class RabbitMqDistributedTracingTests : NewRelicIntegrationTest<RemoteServiceFixtures.RabbitMqBasicMvcFixture>
    {
        // regex for payload
        private const string PayloadRegex = "{\"v\":\\[\\d,\\d\\],\"d\":{\"ty\":\"App\",\"ac\":\"\\d{1,9}\",\"ap\":\"\\d{1,9}\",\"tr\":\"\\w{16,32}\",\"pr\":\\d.\\d{5,6},\"sa\":true,\"ti\":\\d{10,16},\"tk\":\"\\w{0,16}\",\"tx\":\"\\w{16,16}\",\"id\":\"\\w{16,16}\"}}";

        private bool _headerExists;
        private string _headerValue;
        private RabbitMqBasicMvcFixture _fixture;

        public RabbitMqDistributedTracingTests(RemoteServiceFixtures.RabbitMqBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            fixture.TestLogger = output;
            fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();

                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetOrDeleteSpanEventsEnabled(true);
                },
                exerciseApplication: () =>
                {
                    _headerExists = fixture.GetMessageQueue_RabbitMQ_SendReceive_HeaderExists("Test Message");
                    _headerValue = fixture.GetMessageQueue_RabbitMQ_SendReceive_HeaderValue("Test Message");
                    fixture.GetMessageQueue_RabbitMQ_SendReceiveWithEventingConsumer("Test Message");
                }
            );
            fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var bytes = Convert.FromBase64String(_headerValue);
            var decodedString = Encoding.UTF8.GetString(bytes);

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Supportability/DistributedTrace/CreatePayload/Success", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = "Supportability/TraceContext/Create/Success", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = "Supportability/TraceContext/Accept/Success", callCount = 2 }
            };

            var metrics = _fixture.AgentLog.GetMetrics();
            var acctId = _fixture.AgentLog.GetAccountId();
            var appId = _fixture.AgentLog.GetApplicationId();

            // confirms payload exists, all the fields we can validate are correct, and the structure of the json
            NrAssert.Multiple(
                () => Assert.True(_headerExists),
                () => Assert.Contains("\"v\"", decodedString),
                () => Assert.Contains("\"d\"", decodedString),
                () => Assert.Contains("\"ty\":\"App\"", decodedString),
                () => Assert.Contains($"\"ac\":\"{acctId}\"", decodedString),
                () => Assert.Contains($"\"ap\":\"{appId}\"", decodedString),
                () => Assert.Contains("\"pr\"", decodedString),
                () => Assert.Contains("\"sa\":true", decodedString),
                () => Assert.Contains("\"ti\"", decodedString),
                () => Assert.Contains("\"tk\"", decodedString),
                () => Assert.Contains("\"tx\"", decodedString),
                () => Assert.Contains("\"id\"", decodedString),
                () => Assert.Matches(PayloadRegex, decodedString),

                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );

        }
    }
}
