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
public class AwsSdkFirehoseTest : NewRelicIntegrationTest<AwsSdkContainerFirehoseTestFixture>
{
    private readonly AwsSdkContainerFirehoseTestFixture _fixture;

    private readonly string _streamName = $"TestStream-{Guid.NewGuid()}";
    private readonly string _bucketName = $"test-bucket-{Guid.NewGuid()}"; // s3 bucket names can't have capital letters
    private readonly string _recordData = "EtaoinShrdlu";

    public AwsSdkFirehoseTest(AwsSdkContainerFirehoseTestFixture fixture, ITestOutputHelper output) : base(fixture)
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

                _fixture.CreateDeliveryStreamAsync(_streamName, _bucketName);
                _fixture.ListDeliveryStreamsAsync();
                _fixture.PutRecordAsync(_streamName, _recordData);
                _fixture.PutRecordBatchAsync(_streamName, _recordData);
                _fixture.DeleteDeliveryStreamAsync(_streamName);

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

        var metricScopeBase = "WebTransaction/MVC/AwsSdkFirehose/";
        var createDeliveryStreamScope = metricScopeBase + "CreateDeliveryStream/{streamName}/{bucketName}";
        var listDeliveryStreamsScope = metricScopeBase + "ListDeliveryStreams";
        var putRecordScope = metricScopeBase + "PutRecord/{streamName}/{data}";
        var putRecordBatchScope = metricScopeBase + "PutRecordBatch/{streamName}/{data}";
        var deleteDeliveryStreamScope = metricScopeBase + "DeleteDeliveryStream/{streamName}";

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = $"DotNet/Firehose/CreateDeliveryStream/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Firehose/CreateDeliveryStream/{_streamName}", callCount = 1, metricScope = createDeliveryStreamScope},
            new() { metricName = $"DotNet/Firehose/ListDeliveryStreams", callCount = 1},
            new() { metricName = $"DotNet/Firehose/ListDeliveryStreams", callCount = 1, metricScope = listDeliveryStreamsScope},
            new() { metricName = $"DotNet/Firehose/PutRecord/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Firehose/PutRecord/{_streamName}", callCount = 1, metricScope = putRecordScope},
            new() { metricName = $"DotNet/Firehose/PutRecordBatch/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Firehose/PutRecordBatch/{_streamName}", callCount = 1, metricScope = putRecordBatchScope},
            new() { metricName = $"DotNet/Firehose/DeleteDeliveryStream/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Firehose/DeleteDeliveryStream/{_streamName}", callCount = 1, metricScope = deleteDeliveryStreamScope},

        };

        // working with Kinesis in LocalStack, some ARNs match one pattern (region unknown but a real account id) and
        // others match another pattern (region is us-west-2 but account ID is all zeros) so we have to resort to regex matching
        string expectedArnRegex = "arn:aws:firehose:(.+?):([0-9]{12}):deliverystream/" + _streamName;
        var expectedAwsAgentAttributes = new string[]
        {
            "aws.operation", "aws.region", "cloud.resource_id", "cloud.platform"
        };


        // get all kinesis span events so we can verify counts and operations
        var spanEvents = _fixture.AgentLog.GetSpanEvents();

        // ListStreams does not have a stream name or an arn, so there is no way to build the cloud.resource_id attribute for that request type
        // Same for get_records (sadface)
        var firehoseSpanEvents = spanEvents.Where(se => se.AgentAttributes.ContainsKey("cloud.platform") &&
                                                 (string)se.AgentAttributes["cloud.platform"] == "aws_kinesis_delivery_streams" &&
                                                 (string)se.AgentAttributes["aws.operation"] != "list_delivery_streams").ToList();

        Assert.Multiple(
            () => Assert.Equal(0, _fixture.AgentLog.GetWrapperExceptionLineCount()),
            () => Assert.Equal(0, _fixture.AgentLog.GetApplicationErrorLineCount()),

            () => Assert.All(firehoseSpanEvents, se => Assert.Contains(expectedAwsAgentAttributes, key => se.AgentAttributes.ContainsKey(key))),
            () => Assert.All(firehoseSpanEvents, se => Assert.Matches(expectedArnRegex, (string)se.AgentAttributes["cloud.resource_id"])),

            () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
    }
}
