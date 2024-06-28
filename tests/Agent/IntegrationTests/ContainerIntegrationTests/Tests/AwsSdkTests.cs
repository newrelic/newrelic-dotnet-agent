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
    private readonly string _metricScopeBase = "WebTransaction/MVC/AwsSdk/SQS_SendAndReceive/{queueName}";

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
            new Assertions.ExpectedMetric { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}", callCount = 1},
            new Assertions.ExpectedMetric { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}", callCount = 1, metricScope = $"{_metricScopeBase}"},

            new Assertions.ExpectedMetric { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}", callCount = 1},
            new Assertions.ExpectedMetric { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}", callCount = 1, metricScope = $"{_metricScopeBase}"},
        };

        var sendMessageTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_metricScopeBase);

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_metricScopeBase);
        var expectedTransactionTraceSegments = new List<string>
        {
            $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}",
            $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName}"
        };

        Assertions.MetricsExist(expectedMetrics, metrics);
        NrAssert.Multiple(
            () => Assert.True(sendMessageTransactionEvent != null, "sendMessageTransactionEvent should not be null"),
            () => Assert.True(transactionSample != null, "transactionSample should not be null"),
            () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
        );
    }
}
