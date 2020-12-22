// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;


namespace NewRelic.Agent.UnboundedIntegrationTests.RabbitMq
{
    [NetFrameworkTest]
    public abstract class RabbitMqDistributedTracingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly string _sendReceiveQueue = $"integrationTestQueue-{Guid.NewGuid()}";
        private ConsoleDynamicMethodFixture _fixture;

        public RabbitMqDistributedTracingTestsBase(TFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            fixture.TestLogger = output;

            _fixture.AddCommand($"RabbitMQ SendReceive {_sendReceiveQueue} TestMessage");
            _fixture.AddCommand($"RabbitMQ SendReceiveWithEventingConsumer {_sendReceiveQueue} EventingConsumerTestMessage");
            // This is needed to avoid a hang on shutdown in the test app
            _fixture.AddCommand("RabbitMQ Shutdown");

            fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();

                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetOrDeleteSpanEventsEnabled(true);
                }
            );
            fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Supportability/DistributedTrace/CreatePayload/Success", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = "Supportability/TraceContext/Create/Success", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = "Supportability/TraceContext/Accept/Success", callCount = 1 }
            };

            var metrics = _fixture.AgentLog.GetMetrics();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }

    public class RabbitMqDistributedTracingTests : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public RabbitMqDistributedTracingTests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RabbitMqLegacyDistributedTracingTests : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW461>
    {
        public RabbitMqLegacyDistributedTracingTests(ConsoleDynamicMethodFixtureFW461 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
