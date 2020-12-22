// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.RabbitMq
{
    [NetFrameworkTest]
    public abstract class RabbitMqTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixtureFW
    {
        private readonly ConsoleDynamicMethodFixtureFW _fixture;

        private readonly string _sendReceiveQueue = $"integrationTestQueue-{Guid.NewGuid()}";
        private readonly string _purgeQueue = $"integrationPurgeTestQueue-{Guid.NewGuid()}";
        // The topic name has to contain a '.' character.  See https://www.rabbitmq.com/tutorials/tutorial-five-dotnet.html
        private readonly string _sendReceiveTopic = "SendReceiveTopic.Topic";

        private readonly string _metricScopeBase = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.RabbitMQ";

        protected RabbitMqTestsBase(TFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;


            _fixture.AddCommand($"RabbitMQ SendReceive {_sendReceiveQueue} TestMessage");
            _fixture.AddCommand($"RabbitMQ SendReceiveTempQueue TempQueueTestMessage");
            _fixture.AddCommand($"RabbitMQ QueuePurge {_purgeQueue}");
            _fixture.AddCommand($"RabbitMQ SendReceiveTopic TestExchange {_sendReceiveTopic} TopicTestMessage");
            // This is needed to avoid a hang on shutdown in the test app
            _fixture.AddCommand("RabbitMQ Shutdown");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();
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
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_sendReceiveQueue}", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceive"},

                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceive"},

                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_purgeQueue}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Produce/Named/{_purgeQueue}", callCount = 1, metricScope = $"{_metricScopeBase}/QueuePurge" },

                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Purge/Named/{_purgeQueue}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Purge/Named/{_purgeQueue}", callCount = 1, metricScope = $"{_metricScopeBase}/QueuePurge" },

                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Produce/Temp", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Produce/Temp", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveTempQueue"},

                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Topic/Produce/Named/{_sendReceiveTopic}", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Topic/Produce/Named/{_sendReceiveTopic}", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveTopic" },

                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Consume/Temp", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Consume/Temp", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveTempQueue"},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/RabbitMQ/Queue/Consume/Temp", callCount = 1, metricScope = $"{_metricScopeBase}/SendReceiveTopic" },
            };

            var sendReceiveTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SendReceive");
            var sendReceiveTempQueueTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SendReceiveTempQueue");
            var queuePurgeTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/QueuePurge");
            var sendReceiveTopicTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SendReceiveTopic");

            var expectedTransactionTraceSegments = new List<string>
            {
                $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}"
            };

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"{_metricScopeBase}/SendReceive");


            Assertions.MetricsExist(expectedMetrics, metrics);

            NrAssert.Multiple(
                () => Assert.NotNull(sendReceiveTransactionEvent),
                () => Assert.NotNull(sendReceiveTempQueueTransactionEvent),
                () => Assert.NotNull(queuePurgeTransactionEvent),
                () => Assert.NotNull(sendReceiveTopicTransactionEvent),
                () => Assert.NotNull(transactionSample),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );

        }
    }

    public class RabbitMqTests : RabbitMqTestsBase<ConsoleDynamicMethodFixtureFW>
    {
        public RabbitMqTests(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    //public class RabbitMqLegacyTests : RabbitMqTestsBase<RemoteServiceFixtures.RabbitMqLegacyBasicMvcFixture>
    //{
    //    public RabbitMqLegacyTests(RemoteServiceFixtures.RabbitMqLegacyBasicMvcFixture fixture, ITestOutputHelper output)
    //        : base(fixture, output)
    //    {
    //    }
    //}

}
