// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests.AwsSdk;

[Trait("Architecture", "amd64")]
public class AwsSdkKinesisTest : NewRelicIntegrationTest<AwsSdkContainerKinesisTestFixture>
{
    private readonly AwsSdkContainerKinesisTestFixture _fixture;

    private readonly string _streamName = $"TestStream-{Guid.NewGuid()}";
    private readonly string _consumerName = $"TestConsumer-{Guid.NewGuid()}";
    private readonly string _recordData = "MyRecordData";

    private const string _accountId = "520198777664"; // matches the account ID parsed from the fake access key used in AwsSdkKinesisExerciser

    public AwsSdkKinesisTest(AwsSdkContainerKinesisTestFixture fixture, ITestOutputHelper output) : base(fixture)
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
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
            },
            exerciseApplication: () =>
            {
                _fixture.Delay(5);

                _fixture.CreateStreamAsync(_streamName);
                _fixture.ListStreamsAsync();
                _fixture.RegisterStreamConsumerAsync(_streamName, _consumerName);
                _fixture.ListStreamConsumersAsync(_streamName);
                _fixture.PutRecordAsync(_streamName, _recordData);
                _fixture.PutRecordsAsync(_streamName, _recordData);
                _fixture.GetRecordsAsync(_streamName);
                _fixture.DeregisterStreamConsumerAsync(_streamName, _consumerName);
                _fixture.DeleteStreamAsync(_streamName);

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
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var metricScopeBase = "WebTransaction/MVC/AwsSdkKinesis/";
        var createStreamScope = metricScopeBase + "CreateStream/{streamName}";
        var listStreamsScope = metricScopeBase + "ListStreams";
        var registerStreamConsumerScope = metricScopeBase + "RegisterStreamConsumer/{streamName}/{consumerName}";
        var listStreamConsumersScope = metricScopeBase + "ListStreamConsumers/{streamName}";
        var putRecordScope = metricScopeBase + "PutRecord/{streamName}/{data}";
        var putRecordsScope = metricScopeBase + "PutRecords/{streamName}/{data}";
        var getRecordsScope = metricScopeBase + "GetRecords/{streamName}";
        var deregisterStreamConsumerScope = metricScopeBase + "DeregisterStreamConsumer/{streamName}/{consumerName}";
        var deleteStreamScope = metricScopeBase + "DeleteStream/{streamName}";

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = $"DotNet/Kinesis/CreateStream/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/CreateStream/{_streamName}", callCount = 1, metricScope = createStreamScope},
            new() { metricName = $"DotNet/Kinesis/ListStreams", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/ListStreams", callCount = 1, metricScope = listStreamsScope},
            new() { metricName = $"DotNet/Kinesis/RegisterStreamConsumer", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/RegisterStreamConsumer", callCount = 1, metricScope = registerStreamConsumerScope},
            new() { metricName = $"DotNet/Kinesis/ListStreamConsumers", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/ListStreamConsumers", callCount = 1, metricScope = listStreamConsumersScope},
            new() { metricName = $"MessageBroker/Kinesis/Queue/Produce/Named/{_streamName}", callCount = 2}, // one for PutRecordAsync and one for PutRecordsAsync
            new() { metricName = $"MessageBroker/Kinesis/Queue/Produce/Named/{_streamName}", callCount = 1, metricScope = putRecordScope},
            new() { metricName = $"MessageBroker/Kinesis/Queue/Produce/Named/{_streamName}", callCount = 1, metricScope = putRecordsScope},
            new() { metricName = $"MessageBroker/Kinesis/Queue/Consume/Temp", callCount = 1}, //TODO why is this named Temp instead of _streamName?
            new() { metricName = $"MessageBroker/Kinesis/Queue/Consume/Temp", callCount = 1, metricScope = getRecordsScope},
            new() { metricName = $"DotNet/Kinesis/DeregisterStreamConsumer", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/DeregisterStreamConsumer", callCount = 1, metricScope = deregisterStreamConsumerScope},
            new() { metricName = $"DotNet/Kinesis/DeleteStream/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/DeleteStream/{_streamName}", callCount = 1, metricScope = deleteStreamScope},

        };

        string expectedArn = $"arn:aws:kinesis:(unknown):{_accountId}:stream/{_streamName}";
        var expectedAwsAgentAttributes = new string[]
        {
            "aws.operation", "aws.region", "cloud.resource_id"
        };


        // get all kinesis span events so we can verify counts and operations
        var spanEvents = _fixture.AgentLog.GetSpanEvents();

        var kinesisSpanEvents = spanEvents.Where(se => se.IntrinsicAttributes["name"].ToString().StartsWith("DotNet/Kinesis"))
            .ToList();

        Assert.Multiple(
            () => Assert.Equal(0, _fixture.AgentLog.GetWrapperExceptionLineCount()),
            () => Assert.Equal(0, _fixture.AgentLog.GetApplicationErrorLineCount()),

            () => Assert.All(kinesisSpanEvents, se => Assert.Contains(expectedAwsAgentAttributes, key => se.AgentAttributes.ContainsKey(key))),
            () => Assert.All(kinesisSpanEvents, se => Assert.Equal(expectedArn, se.AgentAttributes["cloud.resource_id"])),

            () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
    }
}
