// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.AzureServiceBus;

public abstract class AzureServiceBusW3CTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private readonly string _queueName;

    protected AzureServiceBusW3CTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(1));
        _fixture.TestLogger = output;

        _queueName = $"test-queue-{Guid.NewGuid()}";

        _fixture.AddCommand($"AzureServiceBusExerciser InitializeQueue {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser SendAndReceiveAMessage {_queueName}");
        _fixture.AddCommand($"AzureServiceBusExerciser DeleteQueue {_queueName}");

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

    private readonly string _metricScopeBase = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.AzureServiceBus.AzureServiceBusExerciser";

        [Fact]
        public void Test()
        {
            // attributes

            var headerValueTx = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SendAndReceiveAMessage");

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            var produceSpan = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Contains("MessageBroker/AzureServiceBus/Queue/Produce/Named/"))
                .FirstOrDefault();

            var consumeSpan = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Contains("MessageBroker/AzureServiceBus/Queue/Consume/Named/"))
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

public class AzureServiceBusW3CTestsFWLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public AzureServiceBusW3CTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AzureServiceBusW3CTestsFW462 : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public AzureServiceBusW3CTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AzureServiceBusW3CTestsCoreOldest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public AzureServiceBusW3CTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

public class AzureServiceBusW3CTestsCoreLatest : AzureServiceBusW3CTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public AzureServiceBusW3CTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
