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
    public abstract class RabbitMqW3cTracingTestBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        protected readonly string _metricScopeBase;
        protected readonly TFixture _fixture;
        protected readonly string _exerciser;

        protected RabbitMqW3cTracingTestBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;

            // if fixture is FWLatest or CoreLatest, set _exerciser to RabbitMQ7AndNewer else set it to {_exerciser}
            if (fixture.GetType().Name.Contains("FWLatest") || fixture.GetType().Name.Contains("CoreLatest"))
            {
                _exerciser = "RabbitMQ7AndNewer";
                _fixture.AddCommand($"{_exerciser} Initialize");
            }
            else
            {
                _exerciser = "RabbitMQ6AndOlder";
            }
            _metricScopeBase = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.{_exerciser}";


            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();

                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetOrDeleteSpanEventsEnabled(true);
                }
            );
        }
    }

    public abstract class RabbitMqW3cTracingBasicTestBase<TFixture> : RabbitMqW3cTracingTestBase<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly string _sendReceiveQueue = $"integrationTestQueue-{Guid.NewGuid()}";

        protected RabbitMqW3cTracingBasicTestBase(TFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
            _fixture.AddCommand($"{_exerciser} SendReceive {_sendReceiveQueue} TestMessage");
            // This is needed to avoid a hang on shutdown in the test app
            _fixture.AddCommand($"{_exerciser} Shutdown");

            fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            // attributes

            var headerValueTx = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SendReceive");

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            var produceSpan = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Contains("MessageBroker/RabbitMQ/Queue/Produce/Named/"))
                .FirstOrDefault();

            var consumeSpan = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Contains("MessageBroker/RabbitMQ/Queue/Consume/Named/"))
                .FirstOrDefault();

            Assert.Equal(headerValueTx.IntrinsicAttributes["guid"], produceSpan.IntrinsicAttributes["transactionId"]);
            Assert.Equal(headerValueTx.IntrinsicAttributes["traceId"], produceSpan.IntrinsicAttributes["traceId"]);
            Assert.True(AttributeComparer.IsEqualTo(headerValueTx.IntrinsicAttributes["priority"], produceSpan.IntrinsicAttributes["priority"]),
                $"priority: expected: {headerValueTx.IntrinsicAttributes["priority"]}, actual: {produceSpan.IntrinsicAttributes["priority"]}");

            Assert.Equal(headerValueTx.IntrinsicAttributes["guid"], consumeSpan.IntrinsicAttributes["transactionId"]);
            Assert.Equal(headerValueTx.IntrinsicAttributes["traceId"], consumeSpan.IntrinsicAttributes["traceId"]);
            Assert.True(AttributeComparer.IsEqualTo(headerValueTx.IntrinsicAttributes["priority"], consumeSpan.IntrinsicAttributes["priority"]),
                $"priority: expected: {headerValueTx.IntrinsicAttributes["priority"]}, actual: {consumeSpan.IntrinsicAttributes["priority"]}");

            // metrics

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Supportability/DistributedTrace/CreatePayload/Success", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"Supportability/TraceContext/Create/Success", callCount = 1},
            };

            var metrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }

    [NetFrameworkTest]
    public class RabbitMqW3cTracingBasicTestFW462 : RabbitMqW3cTracingBasicTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public RabbitMqW3cTracingBasicTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqW3cTracingBasicTestFWLatest : RabbitMqW3cTracingBasicTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RabbitMqW3cTracingBasicTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqW3cTracingBasicTestCoreOldest : RabbitMqW3cTracingBasicTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public RabbitMqW3cTracingBasicTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqW3cTracingBasicTestCoreLatest : RabbitMqW3cTracingBasicTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public RabbitMqW3cTracingBasicTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
        }
    }

    public abstract class RabbitMqW3cTracingEventingConsumerTestBase<TFixture> : RabbitMqW3cTracingTestBase<TFixture> where TFixture: ConsoleDynamicMethodFixture
    {
        private readonly string _sendReceiveQueue = $"integrationTestQueue-{Guid.NewGuid()}";

        protected RabbitMqW3cTracingEventingConsumerTestBase(TFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
            _fixture.AddCommand($"{_exerciser} SendReceiveWithEventingConsumer {_sendReceiveQueue} TestMessage");
            // This is needed to avoid a hang on shutdown in the test app
            _fixture.AddCommand($"{_exerciser} Shutdown");

            fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            // transaction attributes

            var produceTx = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SendReceiveWithEventingConsumer");
            var consumeTx = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Message/RabbitMQ/Queue/Named/{_sendReceiveQueue}");

            Assert.Equal(consumeTx.IntrinsicAttributes["traceId"], produceTx.IntrinsicAttributes["traceId"]);
            Assert.True(AttributeComparer.IsEqualTo(produceTx.IntrinsicAttributes["priority"], consumeTx.IntrinsicAttributes["priority"]),
                $"priority: expected: {produceTx.IntrinsicAttributes["priority"]}, actual: {consumeTx.IntrinsicAttributes["priority"]}");
            Assert.Equal(consumeTx.IntrinsicAttributes["parentId"], produceTx.IntrinsicAttributes["guid"]);
            Assert.Equal("AMQP", consumeTx.IntrinsicAttributes["parent.transportType"]);

            // span attributes

            _fixture.AgentLog.GetSpanEvents().ToList().ForEach
                (span =>
                {
                    Assert.Equal(produceTx.IntrinsicAttributes["traceId"], span.IntrinsicAttributes["traceId"]);
                    Assert.True(AttributeComparer.IsEqualTo(produceTx.IntrinsicAttributes["priority"], span.IntrinsicAttributes["priority"]),
                        $"priority: expected: {produceTx.IntrinsicAttributes["priority"]}, actual: {span.IntrinsicAttributes["priority"]}");
                });

            var produceSpan = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/RabbitMQ/Queue/Produce/Named/{_sendReceiveQueue}");
            var consumeSpan = _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/RabbitMQ/Queue/Consume/Named/{_sendReceiveQueue}");

            Assert.Equal(produceTx.IntrinsicAttributes["guid"], produceSpan.IntrinsicAttributes["transactionId"]);
            Assert.Equal(consumeTx.IntrinsicAttributes["guid"], consumeSpan.IntrinsicAttributes["transactionId"]);

            Assert.Equal(consumeTx.IntrinsicAttributes["parentSpanId"], produceSpan.IntrinsicAttributes["guid"]);

            // metrics
            var acctId = _fixture.AgentLog.GetAccountId();
            var appId = _fixture.AgentLog.GetApplicationId();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"DurationByCaller/App/{acctId}/{appId}/AMQP/all", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"DurationByCaller/App/{acctId}/{appId}/AMQP/allOther", callCount = 1},

                new Assertions.ExpectedMetric { metricName = $"TransportDuration/App/{acctId}/{appId}/AMQP/all", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"TransportDuration/App/{acctId}/{appId}/AMQP/allOther", callCount = 1},

                new Assertions.ExpectedMetric { metricName = $"Supportability/DistributedTrace/CreatePayload/Success", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"Supportability/TraceContext/Create/Success", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"Supportability/TraceContext/Accept/Success", callCount = 1}
            };

            var metrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }

    [NetFrameworkTest]
    public class RabbitMqW3cTracingEventingConsumerTestFW462 : RabbitMqW3cTracingEventingConsumerTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public RabbitMqW3cTracingEventingConsumerTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqW3cTracingEventingConsumerTestFWLatest : RabbitMqW3cTracingEventingConsumerTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RabbitMqW3cTracingEventingConsumerTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqW3cTracingEventingConsumerTestCoreOldest : RabbitMqW3cTracingEventingConsumerTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public RabbitMqW3cTracingEventingConsumerTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqW3cTracingEventingConsumerTestCoreLatest : RabbitMqW3cTracingEventingConsumerTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public RabbitMqW3cTracingEventingConsumerTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
        {
        }
    }
}
