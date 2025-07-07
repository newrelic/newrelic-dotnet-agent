// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.AzureServiceBus
{
    public abstract class AzureServiceBusW3CTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly string _queueOrTopicName;
        private readonly string _destinationType;

        protected AzureServiceBusW3CTestsBase(TFixture fixture, string destinationType, ITestOutputHelper output) :
            base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(1));
            _fixture.TestLogger = output;

            _queueOrTopicName = $"test-queue-{Guid.NewGuid()}";
            _destinationType = destinationType;


            _fixture.AddCommand($"AzureServiceBusExerciser Initialize{_destinationType} {_queueOrTopicName}");
            _fixture.AddCommand($"AzureServiceBusExerciser SendAndReceiveAMessageFor{_destinationType} {_queueOrTopicName}");
            _fixture.AddCommand($"AzureServiceBusExerciser Delete{_destinationType} {_queueOrTopicName}");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier.ForceTransactionTraces();

                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetOrDeleteSpanEventsEnabled(true);
                }
            );

            _fixture.Initialize();
        }

        private readonly string _metricScopeBase =
            "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.AzureServiceBus.AzureServiceBusExerciser";

        [Fact]
        public void Test()
        {
            // attributes

            var headerValueTx =
                _fixture.AgentLog.TryGetTransactionEvent(
                    $"{_metricScopeBase}/SendAndReceiveAMessageFor{_destinationType}");

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            // produce is always queue.
            var produceSpan = spanEvents.Where(@event =>
                    @event.IntrinsicAttributes["name"].ToString()
                        .Contains("MessageBroker/ServiceBus/Queue/Produce/Named/"))
                .FirstOrDefault();

            var consumeSpan = spanEvents.Where(@event =>
                    @event.IntrinsicAttributes["name"].ToString()
                        .Contains($"MessageBroker/ServiceBus/{_destinationType}/Consume/Named/"))
                .FirstOrDefault();

            Assert.NotNull(produceSpan);
            Assert.NotNull(consumeSpan);

            Assert.Equal(headerValueTx.IntrinsicAttributes["guid"], produceSpan.IntrinsicAttributes["transactionId"]);
            Assert.Equal(headerValueTx.IntrinsicAttributes["traceId"], produceSpan.IntrinsicAttributes["traceId"]);
            Assert.True(
                AttributeComparer.IsEqualTo(headerValueTx.IntrinsicAttributes["priority"],
                    produceSpan.IntrinsicAttributes["priority"]),
                $"priority: expected: {headerValueTx.IntrinsicAttributes["priority"]}, actual: {produceSpan.IntrinsicAttributes["priority"]}");

            Assert.Equal(headerValueTx.IntrinsicAttributes["guid"], consumeSpan.IntrinsicAttributes["transactionId"]);
            Assert.Equal(headerValueTx.IntrinsicAttributes["traceId"], consumeSpan.IntrinsicAttributes["traceId"]);
            Assert.True(
                AttributeComparer.IsEqualTo(headerValueTx.IntrinsicAttributes["priority"],
                    consumeSpan.IntrinsicAttributes["priority"]),
                $"priority: expected: {headerValueTx.IntrinsicAttributes["priority"]}, actual: {consumeSpan.IntrinsicAttributes["priority"]}");

            // metrics

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric
                {
                    metricName = $"Supportability/DistributedTrace/CreatePayload/Success", callCount = 1
                },
                new Assertions.ExpectedMetric
                {
                    metricName = $"Supportability/TraceContext/Create/Success", callCount = 1
                },
            };

            var metrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }

    #region Queue Tests

    public class AzureServiceBusW3CQueueTestsFWLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AzureServiceBusW3CQueueTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output) : base(fixture, "Queue", output)
        {
        }
    }

    public class AzureServiceBusW3CQueueTestsFW462 : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public AzureServiceBusW3CQueueTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) :
            base(fixture, "Queue", output)
        {
        }
    }

    public class
        AzureServiceBusW3CQueueTestsCoreOldest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public AzureServiceBusW3CQueueTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture,
            ITestOutputHelper output) : base(fixture, "Queue", output)
        {
        }
    }

    public class
        AzureServiceBusW3CQueueTestsCoreLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public AzureServiceBusW3CQueueTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture,
            ITestOutputHelper output) : base(fixture, "Queue", output)
        {
        }
    }

    #endregion Queue Tests

    #region Topic Tests

    public class AzureServiceBusW3CTopicTestsFWLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AzureServiceBusW3CTopicTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output) : base(fixture, "Topic", output)
        {
        }
    }

    public class AzureServiceBusW3CTopicTestsFW462 : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public AzureServiceBusW3CTopicTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) :
            base(fixture, "Topic", output)
        {
        }
    }

    public class
        AzureServiceBusW3CTopicTestsCoreOldest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public AzureServiceBusW3CTopicTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture,
            ITestOutputHelper output) : base(fixture, "Topic", output)
        {
        }
    }

    public class
        AzureServiceBusW3CTopicTestsCoreLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public AzureServiceBusW3CTopicTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture,
            ITestOutputHelper output) : base(fixture, "Topic", output)
        {
        }
    }

    #endregion Topic Tests

}
