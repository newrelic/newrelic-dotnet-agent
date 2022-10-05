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
    public abstract class RabbitMqDistributedTracingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly string _sendReceiveQueue = $"integrationTestQueue-{Guid.NewGuid()}";
        private ConsoleDynamicMethodFixture _fixture;

        public RabbitMqDistributedTracingTestsBase(TFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            fixture.TestLogger = output;

            // RabbitMQ SendRecieve uses the BasicGet method to receive, which does not process incoming tracing payloads
            _fixture.AddCommand($"RabbitMQ SendReceive {_sendReceiveQueue} TestMessage");
            // RabbitMQ SendRecieveWithEventingConsumer uses the HandleBasicDeliverWrapper on the receiving side, which does process incoming tracing headers
            // We execute the method twice to make sure this issue stays fixed: https://github.com/newrelic/newrelic-dotnet-agent/issues/464
            _fixture.AddCommand($"RabbitMQ SendReceiveWithEventingConsumer {_sendReceiveQueue} EventingConsumerTestMessageOne");
            _fixture.AddCommand($"RabbitMQ SendReceiveWithEventingConsumer {_sendReceiveQueue} EventingConsumerTestMessageTwo");
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
                new Assertions.ExpectedMetric { metricName = "Supportability/DistributedTrace/CreatePayload/Success", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = "Supportability/TraceContext/Create/Success", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = "Supportability/TraceContext/Accept/Success", callCount = 2 }
            };

            var metrics = _fixture.AgentLog.GetMetrics();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }

    [NetFrameworkTest]
    public class RabbitMqDistributedTracingFW462Tests : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public RabbitMqDistributedTracingFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqDistributedTracingFW471Tests : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public RabbitMqDistributedTracingFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqDistributedTracingFWLatestTests : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RabbitMqDistributedTracingFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqDistributedTracingNetCore31Tests : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public RabbitMqDistributedTracingNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqDistributedTracingNetCore50Tests : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public RabbitMqDistributedTracingNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqDistributedTracingNetCoreLatestTests : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public RabbitMqDistributedTracingNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
