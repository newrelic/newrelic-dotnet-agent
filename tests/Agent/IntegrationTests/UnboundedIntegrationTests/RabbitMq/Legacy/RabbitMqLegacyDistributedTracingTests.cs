// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.Collections.Generic;
using Xunit;



namespace NewRelic.Agent.UnboundedIntegrationTests.RabbitMq.Legacy
{
    public abstract class RabbitMqLegacyDistributedTracingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly string _sendReceiveQueue = $"integrationTestQueue-{Guid.NewGuid()}";
        private ConsoleDynamicMethodFixture _fixture;

        public RabbitMqLegacyDistributedTracingTestsBase(TFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            fixture.TestLogger = output;

            // RabbitMQ SendRecieve uses the BasicGet method to receive, which does not process incoming tracing payloads
            _fixture.AddCommand($"RabbitMQLegacyExerciser SendReceive {_sendReceiveQueue} TestMessage");
            // RabbitMQ SendRecieveWithEventingConsumer uses the HandleBasicDeliverWrapper on the receiving side, which does process incoming tracing headers
            // We execute the method twice to make sure this issue stays fixed: https://github.com/newrelic/newrelic-dotnet-agent/issues/464
            _fixture.AddCommand($"RabbitMQLegacyExerciser SendReceiveWithEventingConsumer {_sendReceiveQueue} EventingConsumerTestMessageOne");
            _fixture.AddCommand($"RabbitMQLegacyExerciser SendReceiveWithEventingConsumer {_sendReceiveQueue} EventingConsumerTestMessageTwo");
            // This is needed to avoid a hang on shutdown in the test app
            _fixture.AddCommand("RabbitMQLegacyExerciser Shutdown");

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
                new Assertions.ExpectedMetric { metricName = "DotNet/MultiFunctionApplicationHelpers.NetStandardLibraries.RabbitMQ.RabbitMQLegacyExerciser/InstrumentedChildMethod"} ,
                new Assertions.ExpectedMetric { metricName = "DotNet/MultiFunctionApplicationHelpers.NetStandardLibraries.RabbitMQ.RabbitMQLegacyExerciser/InstrumentedChildMethod", metricScope = "OtherTransaction/Message/RabbitMQ/Queue/Named/integrationTestQueue.*", IsRegexScope = true}
            };

            var metrics = _fixture.AgentLog.GetMetrics();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }

    public class RabbitMqLegacyDistributedTracingTestsFWLatest : RabbitMqLegacyDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public RabbitMqLegacyDistributedTracingTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RabbitMqLegacyDistributedTracingTestsFW48 : RabbitMqLegacyDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public RabbitMqLegacyDistributedTracingTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RabbitMqLegacyDistributedTracingTestsFW471 : RabbitMqLegacyDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public RabbitMqLegacyDistributedTracingTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RabbitMqLegacyDistributedTracingTestsFW462 : RabbitMqLegacyDistributedTracingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public RabbitMqLegacyDistributedTracingTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RabbitMqLegacyDistributedTracingTestsCoreOldest : RabbitMqLegacyDistributedTracingTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public RabbitMqLegacyDistributedTracingTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
