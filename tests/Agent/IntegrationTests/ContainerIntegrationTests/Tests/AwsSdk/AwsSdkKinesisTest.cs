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
    //private readonly string _title = "Ghost";
    //private readonly string _year = "1990";

    private const string _accountId = "520056171328"; // matches the account ID parsed from the fake access key used in AwsSdkKinesisExerciser

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
        //var scanScope = metricScopeBase + "Scan/{tableName}";
        //var deleteTableScope = metricScopeBase + "DeleteTable/{tableName}";
        //var putItemScope = metricScopeBase + "PutItem/{tableName}/{title}/{year}";
        //var getItemScope = metricScopeBase + "GetItem/{tableName}/{title}/{year}";
        //var updateItemScope = metricScopeBase + "UpdateItem/{tableName}/{title}/{year}";
        //var deleteItemScope = metricScopeBase + "DeleteItem/{tableName}/{title}/{year}";
        //var queryScope = metricScopeBase + "Query/{tableName}/{title}/{year}";

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = $"DotNet/Kinesis/CreateStream/{_streamName}", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/CreateStream/{_streamName}", callCount = 1, metricScope = createStreamScope},
            new() { metricName = $"DotNet/Kinesis/ListStreams", callCount = 1},
            new() { metricName = $"DotNet/Kinesis/ListStreams", callCount = 1, metricScope = listStreamsScope},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/put_item", callCount = 1},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/put_item", callCount = 1, metricScope = putItemScope},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/get_item", callCount = 1},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/get_item", callCount = 1, metricScope = getItemScope},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/update_item", callCount = 1},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/update_item", callCount = 1, metricScope = updateItemScope},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/delete_item", callCount = 1},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/delete_item", callCount = 1, metricScope = deleteItemScope},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/query", callCount = 1},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/query", callCount = 1, metricScope = queryScope},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/scan", callCount = 1},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/scan", callCount = 1, metricScope = scanScope},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/delete_table", callCount = 1},
            //new() { metricName = $"Datastore/statement/Kinesis/{_streamName}/delete_table", callCount = 1, metricScope = deleteTableScope},

        };

        //var expectedOperations = new[] { "create_table", "describe_table", "put_item", "get_item", "update_item", "delete_item", "query", "scan", "delete_table" };
        //var expectedOperationsCount = expectedOperations.Length;

        //string expectedArn = $"arn:aws:dynamodb:(unknown):{_accountId}:table/{_streamName}";
        //var expectedAwsAgentAttributes = new string[]
        //{
        //    "aws.operation", "aws.requestId", "aws.region", "cloud.resource_id",
        //};


        //// get all datastore span events so we can verify counts and operations
        //var datastoreSpanEvents = _fixture.AgentLog.GetSpanEvents()
        //    .Where(se => (string)se.IntrinsicAttributes["category"] == "datastore")
        //    .ToList();

        //// select the set of AgentAttributes values with a key of "aws.operation"
        //var awsOperations = datastoreSpanEvents.Select(se => (string)se.AgentAttributes["aws.operation"]).ToList();


        Assert.Multiple(
            () => Assert.Equal(0, _fixture.AgentLog.GetWrapperExceptionLineCount()),
            () => Assert.Equal(0, _fixture.AgentLog.GetApplicationErrorLineCount()),

            //() => Assert.Equal(expectedOperationsCount, datastoreSpanEvents.Count),
            //() => Assert.Equal(expectedOperationsCount, awsOperations.Intersect(expectedOperations).Count()),

            //() => Assert.All(datastoreSpanEvents, se => Assert.Contains(expectedAwsAgentAttributes, key => se.AgentAttributes.ContainsKey(key))),
            //() => Assert.All(datastoreSpanEvents, se => Assert.Equal(expectedArn, se.AgentAttributes["cloud.resource_id"])),

            () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
    }
}
