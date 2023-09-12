// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.RabbitMq
{
    public abstract class RabbitMqTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private readonly string _sendReceiveQueue = $"integrationTestQueue-{Guid.NewGuid()}";
        private readonly string _purgeQueue = $"integrationPurgeTestQueue-{Guid.NewGuid()}";
        private readonly string _testExchangeName = $"integrationTestExchange-{Guid.NewGuid()}";
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
            _fixture.AddCommand($"RabbitMQ SendReceiveTopic {_testExchangeName} {_sendReceiveTopic} TopicTestMessage");
            // This is needed to avoid a hang on shutdown in the test app
            _fixture.AddCommand("RabbitMQ Shutdown");

            // AddActions() executes the applied actions after actions defined by the base.
            // In this case the base defines an exerciseApplication action we want to wait after.
            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
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
                () => Assert.True(sendReceiveTransactionEvent != null, "sendReceiveTransactionEvent should not be null"),
                () => Assert.True(sendReceiveTempQueueTransactionEvent != null, "sendReceiveTempQueueTransactionEvent should not be null"),
                () => Assert.True(queuePurgeTransactionEvent != null, "queuePurgeTransactionEvent should not be null"),
                () => Assert.True(sendReceiveTopicTransactionEvent != null, "sendReceiveTopicTransactionEvent should not be null"),
                () => Assert.True(transactionSample != null, "transactionSample should not be null"),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );

        }
    }

    [NetFrameworkTest]
    public class RabbitMqTestsFWLatest : RabbitMqTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RabbitMqTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqTestsFW48 : RabbitMqTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public RabbitMqTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqTestsFW471 : RabbitMqTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public RabbitMqTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqTestsFW462 : RabbitMqTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public RabbitMqTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqTestsCoreLatest : RabbitMqTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public RabbitMqTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqTestsCoreOldest : RabbitMqTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public RabbitMqTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
