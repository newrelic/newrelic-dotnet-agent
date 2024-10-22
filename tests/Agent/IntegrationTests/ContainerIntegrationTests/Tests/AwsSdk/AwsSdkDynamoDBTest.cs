// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests.AwsSdk;

public abstract class AwsSdkDynamoDBTestBase : NewRelicIntegrationTest<AwsSdkContainerDynamoDBTestFixture>
{
    private readonly AwsSdkContainerDynamoDBTestFixture _fixture;

    //private readonly string _metricScope1 = "WebTransaction/MVC/AwsSdkDynamoDB/PutItemAsync/{queueName}"; // todo fix this
    private bool _initCollections;

    protected AwsSdkDynamoDBTestBase(AwsSdkContainerDynamoDBTestFixture fixture, ITestOutputHelper output, bool initCollections) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _initCollections = initCollections;

        _fixture.SetAdditionalEnvironmentVariable("AWSSDK_INITCOLLECTIONS", initCollections.ToString());


        // todo: is all of this necessary?
        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.ForceTransactionTraces();
                configModifier.EnableDistributedTrace();
                configModifier.ConfigureFasterMetricsHarvestCycle(15);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(15);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                configModifier.LogToConsole();

            },
            exerciseApplication: () =>
            {
                _fixture.Delay(5);

                _fixture.CreateTableAsync("Movies");
                _fixture.PutItemAsync("Movies", "Ghost", 1989);

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
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
        Assert.Equal(0, _fixture.AgentLog.GetWrapperExceptionLineCount());
        Assert.Equal(0, _fixture.AgentLog.GetApplicationErrorLineCount());

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        //var expectedMetrics = new List<Assertions.ExpectedMetric>
        //{
        //    new() { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName1}", callCount = 2}, // SendMessage and SendMessageBatch
        //    new() { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName1}", callCount = 2, metricScope = _metricScope1},
        //    new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName1}", callCount = 2},
        //    new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName1}", callCount = 2, metricScope = _metricScope1},
        //    new() { metricName = $"MessageBroker/SQS/Queue/Purge/Named/{_testQueueName1}", callCount = 1},
        //    new() { metricName = $"MessageBroker/SQS/Queue/Purge/Named/{_testQueueName1}", callCount = 1, metricScope = _metricScope1},

        //    new() { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName2}", callCount = 1},
        //    new() { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName2}", callCount = 1, metricScope = _metricScope2},
        //    new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName2}", callCount = 1},
        //    new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName2}", callCount = 1, metricScope = "OtherTransaction/Custom/AwsSdkTestApp.SQSBackgroundService.SQSReceiverService/ProcessRequestAsync"},

        //    // Only consume metrics for queue 3
        //    new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName3}", callCount = 1},
        //    new() { metricName = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName3}", callCount = 1, metricScope = "OtherTransaction/Custom/AwsSdkTestApp.SQSBackgroundService.SQSReceiverService/ProcessRequestAsync"},

        //};

        //// If the AWS SDK is configured to NOT initialize empty collections, trace headers will not be accepted
        //if (_initCollections)
        //{
        //    expectedMetrics.Add(new() { metricName = "Supportability/TraceContext/Accept/Success", callCount = 1 });
        //}

        //var sendReceivePurgeTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_metricScope1);
        //var sendReceivePurgeTransactionSample = _fixture.AgentLog.TryGetTransactionSample(_metricScope1);
        //var sendReceivePurgeExpectedTransactionTraceSegments = new List<string>
        //{
        //    $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName1}",
        //    $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName1}",
        //    $"MessageBroker/SQS/Queue/Purge/Named/{_testQueueName1}"
        //};

        //Assertions.MetricsExist(expectedMetrics, metrics);
        //NrAssert.Multiple(
        //    () => Assert.True(sendReceivePurgeTransactionEvent != null, "sendReceivePurgeTransactionEvent should not be null"),
        //    () => Assert.True(sendReceivePurgeTransactionSample != null, "sendReceivePurgeTransactionSample should not be null"),
        //    () => Assertions.TransactionTraceSegmentsExist(sendReceivePurgeExpectedTransactionTraceSegments, sendReceivePurgeTransactionSample)
        //);

        //var sendMessageToQueueTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_metricScope2);
        //var receiveMessageTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Custom/AwsSdkTestApp.SQSBackgroundService.SQSReceiverService/ProcessRequestAsync");
        //NrAssert.Multiple(
        //    () => Assert.True(sendMessageToQueueTransactionEvent != null, "sendMessageToQueueTransactionEvent should not be null"),
        //    () => Assert.True(receiveMessageTransactionEvent != null, "receiveMessageTransactionEvent should not be null")
        //);

        //var queueProduce = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName2}";
        //var queueConsume = $"MessageBroker/SQS/Queue/Consume/Named/{_testQueueName2}";

        //var spans = _fixture.AgentLog.GetSpanEvents().ToList();
        //var produceSpan = spans.LastOrDefault(s => s.IntrinsicAttributes["name"].Equals(queueProduce));
        //var consumeSpan = spans.LastOrDefault(s => s.IntrinsicAttributes["name"].Equals(queueConsume));

        //NrAssert.Multiple(
        //    () => Assert.True(produceSpan != null, "produceSpan should not be null"),
        //    () => Assert.True(consumeSpan != null, "consumeSpan should not be null"),
        //    () => Assert.True(produceSpan!.IntrinsicAttributes.ContainsKey("traceId")),
        //    () => Assert.True(produceSpan!.IntrinsicAttributes.ContainsKey("guid")),
        //    () => Assert.True(consumeSpan!.IntrinsicAttributes.ContainsKey("traceId"))
        //);

        //if (_initCollections)
        //{
        //    // verify that distributed trace worked as expected -- the last produce span should have the same traceId and parentId as the last consume span
        //    var processRequestSpan = spans.LastOrDefault(s => s.IntrinsicAttributes["name"].Equals("OtherTransaction/Custom/AwsSdkTestApp.SQSBackgroundService.SQSReceiverService/ProcessRequestAsync") && s.IntrinsicAttributes.ContainsKey("parentId"));

        //    NrAssert.Multiple(
        //        () => Assert.True(processRequestSpan != null, "processRequestSpan should not be null"),
        //        () => Assert.Equal(produceSpan!.IntrinsicAttributes["traceId"], consumeSpan!.IntrinsicAttributes["traceId"]),
        //        () => Assert.Equal(produceSpan!.IntrinsicAttributes["guid"], processRequestSpan!.IntrinsicAttributes["parentId"])
        //    );
        //}

        //NrAssert.Multiple(
        //    // entity relationship attributes
        //    () => Assert.Equal(produceSpan!.AgentAttributes["messaging.system"], "aws_sqs"),
        //    () => Assert.Equal(produceSpan!.AgentAttributes["messaging.destination.name"], _testQueueName2),
        //    () => Assert.Equal(consumeSpan!.AgentAttributes["cloud.account.id"], "000000000000"),
        //    () => Assert.Equal(consumeSpan!.AgentAttributes["cloud.region"], "us-west-2"),
        //    () => Assert.Equal(consumeSpan!.AgentAttributes["messaging.system"], "aws_sqs"),
        //    () => Assert.Equal(consumeSpan!.AgentAttributes["messaging.destination.name"], _testQueueName2),
        //    () => Assert.Equal(consumeSpan!.AgentAttributes["cloud.account.id"], "000000000000"),
        //    () => Assert.Equal(consumeSpan!.AgentAttributes["cloud.region"], "us-west-2")
        //);
    }
}

public class AwsSdkDynamoDBTestInitializedCollections : AwsSdkDynamoDBTestBase
{
    public AwsSdkDynamoDBTestInitializedCollections(AwsSdkContainerDynamoDBTestFixture fixture, ITestOutputHelper output) : base(fixture, output, true)
    {
    }
}
public class AwsSdkDynamoDBTestNullCollections : AwsSdkDynamoDBTestBase
{
    public AwsSdkDynamoDBTestNullCollections(AwsSdkContainerDynamoDBTestFixture fixture, ITestOutputHelper output) : base(fixture, output, false)
    {
    }
}

