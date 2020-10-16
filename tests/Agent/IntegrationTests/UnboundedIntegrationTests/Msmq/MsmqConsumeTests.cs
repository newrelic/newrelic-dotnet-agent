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
    /// <summary>
    /// Integration Test for MSMQ Consume message.
    /// </summary>
    /// <remarks>
    /// Although this test also uses the MSMQ Send endpoint, it is important to keep the Send and Consume tests as separate fixtures
    /// in order to separately test the existence of a Consume segment in the transaction trace. Only a single TT is being saved per test.
    /// </remarks>
    [NetFrameworkTest]
    public class MsmqConsumeTests : IClassFixture<RemoteServiceFixtures.MSMQBasicMVCApplicationFixture>
    {
        private readonly RemoteServiceFixtures.MSMQBasicMVCApplicationFixture _fixture;

        public MsmqConsumeTests(RemoteServiceFixtures.MSMQBasicMVCApplicationFixture fixture, ITestOutputHelper output)
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
                    _fixture.GetMessageQueue_Msmq_Receive();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/Msmq/Queue/Consume/Named/private$\nrtestqueue", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/Msmq/Queue/Consume/Named/private$\nrtestqueue", callCount = 1, metricScope = "WebTransaction/MVC/MSMQController/Msmq_Receive"}
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/Msmq/Queue/Consume/Named/private$\nrtestqueue"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/MSMQController/Msmq_Receive");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/MSMQController/Msmq_Receive");

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
