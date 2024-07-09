// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

public class AwsSdkSQSTest : NewRelicIntegrationTest<AwsSdkContainerSQSTestFixture>
{
    private readonly AwsSdkContainerSQSTestFixture _fixture;

    private readonly string _testQueueName = $"TestQueue-{Guid.NewGuid()}";
    private readonly string _metricScope1 = "WebTransaction/MVC/AwsSdk/SQS_SendReceivePurge/{queueName}";
    private string _messagesJson;

    public AwsSdkSQSTest(AwsSdkContainerSQSTestFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;


        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.ForceTransactionTraces();
                configModifier.EnableDistributedTrace();
                configModifier.ConfigureFasterMetricsHarvestCycle(15);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(15);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(20);
                configModifier.LogToConsole();

            },
            exerciseApplication: () =>
            {
                _fixture.Delay(5);

                _fixture.ExerciseSQS_SendReceivePurge(_testQueueName);
                _messagesJson = _fixture.ExerciseSQS_SendAndReceiveInSeparateTransactions(_testQueueName);

                _fixture.Delay(15);

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2));

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }


    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}", callCount = 3}, // SendMessage and SendMessageBatch
            new() { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}", callCount = 2, metricScope = _metricScope1},
            new() { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}", callCount = 1, metricScope = "WebTransaction/MVC/AwsSdk/SQS_SendMessageToQueue/{message}/{messageQueueUrl}"},


            new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}", callCount = 3},
            new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}", callCount = 2, metricScope = _metricScope1},
            new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}", callCount = 1, metricScope = "OtherTransaction/Custom/AwsSdkTestApp.SQSReceiverService/ProcessRequestAsync"},

            new() { metricName = $"MessageBroker/SQS/Queue/Purge/Named/{_testQueueName}", callCount = 1},
            new() { metricName = $"MessageBroker/SQS/Queue/Purge/Named/{_testQueueName}", callCount = 1, metricScope = _metricScope1},
        };

        var sendMessageTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_metricScope1);

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_metricScope1);
        var expectedTransactionTraceSegments = new List<string>
        {
            $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}",
            $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}",
            $"MessageBroker/SQS/Queue/Purge/Named/{_testQueueName}"
        };

        Assertions.MetricsExist(expectedMetrics, metrics);
        NrAssert.Multiple(
            () => Assert.True(sendMessageTransactionEvent != null, "sendMessageTransactionEvent should not be null"),
            () => Assert.True(transactionSample != null, "transactionSample should not be null"),
            () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
        );

        // verify that distributed trace worked as expected -- the last produce span should have the same traceId and parentId as the last consume span
        var queueProduce = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}";
        var queueConsume = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}";

        var spans = _fixture.AgentLog.GetSpanEvents().ToList();
        var produceSpan = spans.LastOrDefault(s => s.IntrinsicAttributes["name"].Equals(queueProduce));
        var consumeSpan = spans.LastOrDefault(s => s.IntrinsicAttributes["name"].Equals(queueConsume));

        NrAssert.Multiple(
            () => Assert.NotNull(produceSpan),
            () => Assert.NotNull(consumeSpan),
            () => Assert.True(produceSpan!.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(produceSpan!.IntrinsicAttributes.ContainsKey("parentId")),
            () => Assert.True(consumeSpan!.IntrinsicAttributes.ContainsKey("traceId")),
            () => Assert.True(consumeSpan!.IntrinsicAttributes.ContainsKey("parentId")),
            () => Assert.Equal(produceSpan!.IntrinsicAttributes["traceId"], consumeSpan!.IntrinsicAttributes["traceId"]),
            () => Assert.Equal(produceSpan!.IntrinsicAttributes["parentId"], consumeSpan!.IntrinsicAttributes["parentId"])
        );
    }
}
