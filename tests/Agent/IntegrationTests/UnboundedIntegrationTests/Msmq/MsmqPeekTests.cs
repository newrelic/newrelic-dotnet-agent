// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Msmq
{
    [NetFrameworkTest]
    public class MsmqPeekTests : NewRelicIntegrationTest<RemoteServiceFixtures.MSMQBasicMVCApplicationFixture>
    {
        private readonly RemoteServiceFixtures.MSMQBasicMVCApplicationFixture _fixture;

        public MsmqPeekTests(RemoteServiceFixtures.MSMQBasicMVCApplicationFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.GetMessageQueue_Msmq_Send(true);
                    _fixture.GetMessageQueue_Msmq_Peek();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/Msmq/Queue/Peek/Named/private$\nrtestqueue", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/Msmq/Queue/Peek/Named/private$\nrtestqueue", callCount = 1, metricScope = "WebTransaction/MVC/MSMQController/Msmq_Peek"}
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/Msmq/Queue/Peek/Named/private$\nrtestqueue"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/MSMQController/Msmq_Peek");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/MSMQController/Msmq_Peek");

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
