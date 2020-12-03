// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.RabbitMq
{
    [NetFrameworkTest]
    public class RabbitMqTests : NewRelicIntegrationTest<RemoteServiceFixtures.RabbitMqBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.RabbitMqBasicMvcFixture _fixture;

        private string _sendReceiveQueue;
        private string _purgeQueue;

        public RabbitMqTests(RemoteServiceFixtures.RabbitMqBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _sendReceiveQueue = _fixture.GetMessageQueue_RabbitMQ_SendReceive("Test Message");
                    _fixture.GetMessageQueue_RabbitMQ_SendReceiveTempQueue("Test Message");
                    _purgeQueue = _fixture.GetMessageQueue_RabbitMQ_Purge();
                    _fixture.GetMessageQueue_RabbitMQ_SendReceiveTopic("SendReceiveTopic.Topic", "Test Message");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_sendReceiveQueue}", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_sendReceiveQueue}", callCount = 1, metricScope = "WebTransaction/MVC/RabbitMQController/RabbitMQ_SendReceive"},

                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}", callCount = 1, metricScope = "WebTransaction/MVC/RabbitMQController/RabbitMQ_SendReceive"},

                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_purgeQueue}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_purgeQueue}", callCount = 1, metricScope = "WebTransaction/MVC/RabbitMQController/RabbitMQ_QueuePurge" },

                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Purge/Named/{_purgeQueue}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Purge/Named/{_purgeQueue}", callCount = 1, metricScope = "WebTransaction/MVC/RabbitMQController/RabbitMQ_QueuePurge" },

                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Produce/Temp", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Produce/Temp", callCount = 1, metricScope = "WebTransaction/MVC/RabbitMQController/RabbitMQ_SendReceiveTempQueue"},

                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Topic/Produce/Named/SendReceiveTopic.Topic", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Topic/Produce/Named/SendReceiveTopic.Topic", callCount = 1, metricScope = "WebTransaction/MVC/RabbitMQController/RabbitMQ_SendReceiveTopic" },

                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Consume/Temp", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Consume/Temp", callCount = 1, metricScope = "WebTransaction/MVC/RabbitMQController/RabbitMQ_SendReceiveTempQueue"},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Consume/Temp", callCount = 1, metricScope = "WebTransaction/MVC/RabbitMQController/RabbitMQ_SendReceiveTopic" },
            };

            var sendReceiveTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/RabbitMQController/RabbitMQ_SendReceive");
            var sendReceiveTempQueueTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/RabbitMQController/RabbitMQ_SendReceiveTempQueue");
            var queuePurgeTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/RabbitMQController/RabbitMQ_QueuePurge");
            var sendReceiveTopicTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/RabbitMQController/RabbitMQ_SendReceiveTopic");

            Assertions.MetricsExist(expectedMetrics, metrics);

            NrAssert.Multiple(
                () => Assert.NotNull(sendReceiveTransactionEvent),
                () => Assert.NotNull(sendReceiveTempQueueTransactionEvent),
                () => Assert.NotNull(queuePurgeTransactionEvent),
                () => Assert.NotNull(sendReceiveTopicTransactionEvent)
            );

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}
