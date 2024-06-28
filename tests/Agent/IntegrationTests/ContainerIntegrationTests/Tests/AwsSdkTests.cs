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
    private readonly string _metricScope = "WebTransaction/MVC/AwsSdk/SQS_SendReceivePurge/{queueName}";

    public AwsSdkSQSTest(AwsSdkContainerSQSTestFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;


        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("debug");
                configModifier.ForceTransactionTraces();
                configModifier.ConfigureFasterMetricsHarvestCycle(15);
                configModifier.LogToConsole();

            },
            exerciseApplication: () =>
            {
                _fixture.Delay(15);
                _fixture.ExerciseSQS(_testQueueName);

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));

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
            new() { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}", callCount = 2}, // SendMessage and SendMessageBatch
            new() { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}", callCount = 2, metricScope = _metricScope},

            new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}", callCount = 1},
            new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}", callCount = 1, metricScope = _metricScope},

            new() { metricName = $"MessageBroker/SQS/Queue/Purge/Named/{_testQueueName}", callCount = 1},
            new() { metricName = $"MessageBroker/SQS/Queue/Purge/Named/{_testQueueName}", callCount = 1, metricScope = _metricScope},
        };

        var sendMessageTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_metricScope);

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_metricScope);
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
    }
}
