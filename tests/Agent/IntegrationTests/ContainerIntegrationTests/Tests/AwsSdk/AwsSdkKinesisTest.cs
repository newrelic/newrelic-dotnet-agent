// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests.AwsSdk;

[Trait("Architecture", "amd64")]
[Trait("Distro", "Ubuntu")]
public class AwsSdkKinesisTest : NewRelicIntegrationTest<AwsSdkContainerKinesisTestFixture>
{
    private readonly AwsSdkContainerKinesisTestFixture _fixture;

    private readonly string _streamName = $"TestStream-{Guid.NewGuid()}";
    private readonly string _consumerName = $"TestConsumer-{Guid.NewGuid()}";
    private readonly string _recordData = "MyRecordData";

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
            new() { metricName = $"DotNet/Kinesis/RegisterStreamConsumer/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/RegisterStreamConsumer/{_streamName}", callCount = 1, metricScope = registerStreamConsumerScope},
            new() { metricName = $"DotNet/Kinesis/ListStreamConsumers/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/ListStreamConsumers/{_streamName}", callCount = 1, metricScope = listStreamConsumersScope},
            new() { metricName = $"MessageBroker/Kinesis/Queue/Produce/Named/{_streamName}", callCount = 2}, // one for PutRecordAsync and one for PutRecordsAsync
            new() { metricName = $"MessageBroker/Kinesis/Queue/Produce/Named/{_streamName}", callCount = 1, metricScope = putRecordScope},
            new() { metricName = $"MessageBroker/Kinesis/Queue/Produce/Named/{_streamName}", callCount = 1, metricScope = putRecordsScope},
            new() { metricName = $"MessageBroker/Kinesis/Queue/Consume/Named/Unknown", callCount = 1}, // The instrumentation is unable to get the stream name from GetRecords requests
            new() { metricName = $"MessageBroker/Kinesis/Queue/Consume/Named/Unknown", callCount = 1, metricScope = getRecordsScope},
            new() { metricName = $"DotNet/Kinesis/DeregisterStreamConsumer/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/DeregisterStreamConsumer/{_streamName}", callCount = 1, metricScope = deregisterStreamConsumerScope},
            new() { metricName = $"DotNet/Kinesis/DeleteStream/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/DeleteStream/{_streamName}", callCount = 1, metricScope = deleteStreamScope},

        };

        // working with Kinesis in LocalStack, some ARNs match one pattern (region unknown but a real account id) and
        // others match another pattern (region is us-west-2 but account ID is all zeros) so we have to resort to regex matching
        string expectedArnRegex = "arn:aws:kinesis:(.+?):([0-9]{12}):stream/" + _streamName;
        var expectedAwsAgentAttributes = new string[]
        {
            "aws.operation", "aws.region", "cloud.resource_id", "cloud.platform"
        };


        // get all kinesis span events so we can verify counts and operations
        var spanEvents = _fixture.AgentLog.GetSpanEvents();

        // ListStreams does not have a stream name or an arn, so there is no way to build the cloud.resource_id attribute for that request type
        // Same for get_records (sadface)
        var kinesisSpanEvents = spanEvents.Where(se => se.AgentAttributes.ContainsKey("cloud.platform") &&
                                                 (string)se.AgentAttributes["cloud.platform"] == "aws_kinesis_data_streams" &&
                                                 (string)se.AgentAttributes["aws.operation"] != "list_streams" &&
                                                 (string)se.AgentAttributes["aws.operation"] != "get_records").ToList();

        Assert.Multiple(
            () => Assert.Equal(0, _fixture.AgentLog.GetWrapperExceptionLineCount()),
            () => Assert.Equal(0, _fixture.AgentLog.GetApplicationErrorLineCount()),

            () => Assert.All(kinesisSpanEvents, se => Assert.Contains(expectedAwsAgentAttributes, key => se.AgentAttributes.ContainsKey(key))),
            () => Assert.All(kinesisSpanEvents, se => Assert.Matches(expectedArnRegex, (string)se.AgentAttributes["cloud.resource_id"])),

            () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
    }
}
