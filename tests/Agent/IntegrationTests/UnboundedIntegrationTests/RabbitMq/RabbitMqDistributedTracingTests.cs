// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
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
        private string _exerciser;

        public RabbitMqDistributedTracingTestsBase(TFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            fixture.TestLogger = output;

            // if fixture is FWLatest or CoreLatest, set _exerciser to RabbitMQ7AndNewer else set it to RabbitMQ6AndOlder
            if (fixture.GetType().Name.Contains("FWLatest") || fixture.GetType().Name.Contains("CoreLatest"))
            {
                _exerciser = "RabbitMQ7AndNewer";
                _fixture.AddCommand($"{_exerciser} Initialize");
            }
            else
            {
                _exerciser = "RabbitMQ6AndOlder";
            }

            // RabbitMQ SendRecieve uses the BasicGet method to receive, which does not process incoming tracing payloads
            _fixture.AddCommand($"{_exerciser} SendReceive {_sendReceiveQueue} TestMessage");
            // RabbitMQ SendRecieveWithEventingConsumer uses the HandleBasicDeliverWrapper on the receiving side, which does process incoming tracing headers
            // We execute the method twice to make sure this issue stays fixed: https://github.com/newrelic/newrelic-dotnet-agent/issues/464
            _fixture.AddCommand($"{_exerciser} SendReceiveWithEventingConsumer {_sendReceiveQueue} EventingConsumerTestMessageOne");
            _fixture.AddCommand($"{_exerciser} SendReceiveWithEventingConsumer {_sendReceiveQueue} EventingConsumerTestMessageTwo");
            // This is needed to avoid a hang on shutdown in the test app
            _fixture.AddCommand($"{_exerciser} Shutdown");

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
                new Assertions.ExpectedMetric { metricName = "Supportability/TraceContext/Accept/Success", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $"DotNet/MultiFunctionApplicationHelpers.NetStandardLibraries.{_exerciser}/InstrumentedChildMethod"} ,
                new Assertions.ExpectedMetric { metricName = $"DotNet/MultiFunctionApplicationHelpers.NetStandardLibraries.{_exerciser}/InstrumentedChildMethod", metricScope = "OtherTransaction/Message/RabbitMQ/Queue/Named/integrationTestQueue.*", IsRegexScope = true}
            };

            var metrics = _fixture.AgentLog.GetMetrics();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }

    [NetFrameworkTest]
    public class RabbitMqDistributedTracingTestsFWLatest : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RabbitMqDistributedTracingTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqDistributedTracingTestsFW48 : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public RabbitMqDistributedTracingTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqDistributedTracingTestsFW471 : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public RabbitMqDistributedTracingTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class RabbitMqDistributedTracingTestsFW462 : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public RabbitMqDistributedTracingTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqDistributedTracingTestsCoreLatest : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public RabbitMqDistributedTracingTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class RabbitMqDistributedTracingTestsCoreOldest : RabbitMqDistributedTracingTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public RabbitMqDistributedTracingTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
