// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.RabbitMq
{
    [NetFrameworkTest]
    public class RabbitMqReceiveTests : IClassFixture<RemoteServiceFixtures.RabbitMqReceiverFixture>
    {
        private readonly RemoteServiceFixtures.RabbitMqReceiverFixture _fixture;

        public RabbitMqReceiveTests(RemoteServiceFixtures.RabbitMqReceiverFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.CommandLineArguments = $"--queue={_fixture.QueueName}";

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = _fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.SetLogLevel("all");
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.CreateQueueAndSendMessage();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(30));
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
                new Assertions.ExpectedMetric { metricName = "OtherTransaction/all", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_fixture.QueueName}", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_fixture.QueueName}", callCount = 1, metricScope = $"OtherTransaction/Message/RabbitMQ/Queue/Named/{_fixture.QueueName}"},
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_fixture.QueueName}"
            };

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"OtherTransaction/Message/RabbitMQ/Queue/Named/{_fixture.QueueName}");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Message/RabbitMQ/Queue/Named/{_fixture.QueueName}");

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
