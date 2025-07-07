// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests.AwsSdk;

[Trait("Architecture", "amd64")]
public class AwsSdkMultiServiceTest : NewRelicIntegrationTest<AwsSdkContainerMultiServiceTestFixture>
{
    private readonly AwsSdkContainerMultiServiceTestFixture _fixture;

    private readonly string _tableName = $"TableName_{Guid.NewGuid()}";
    private readonly string _queueName = $"QueueName_{Guid.NewGuid()}";
    private readonly string _bookName = $"BookName_{Guid.NewGuid()}";

    private const string _expectedAccountId = "520056171328"; // matches the account ID parsed from the fake access key used in AwsSdkDynamoDBExerciser
    private const string _unxpectedAccountId = "520198777664"; // matches the account ID parsed from the fake access key used in AwsSdkSQSExerciser


    public AwsSdkMultiServiceTest(AwsSdkContainerMultiServiceTestFixture fixture, ITestOutputHelper output) : base(fixture)
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

                _fixture.ExerciseMultiService(_tableName, _queueName, _bookName);

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex,
                    TimeSpan.FromMinutes(2));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        // get all span events
        var spanEvents = _fixture.AgentLog.GetSpanEvents();
        // select all span events having an Agent attribute with a key of "cloud.resource_id"
        var cloudResourceIdSpanEvents = spanEvents.Where(spanEvent => spanEvent.AgentAttributes.ContainsKey("cloud.resource_id")).ToList();

        string expectedArn = $"arn:aws:dynamodb:(unknown):{_expectedAccountId}:table/{_tableName}";
        string unExpectedArn = $"arn:aws:dynamodb:(unknown):{_unxpectedAccountId}:table/{_tableName}";

        // verify all span events contain the expected arn, and do not contain the unexpected arn and all are of category datastore
        Assert.Multiple(
            () => Assert.All(cloudResourceIdSpanEvents, se => Assert.Equal(expectedArn, se.AgentAttributes["cloud.resource_id"])),
            () => Assert.All(cloudResourceIdSpanEvents, se => Assert.NotEqual(_unxpectedAccountId, se.AgentAttributes["cloud.resource_id"])),
            () => Assert.All(cloudResourceIdSpanEvents, se => Assert.Equal("datastore", se.IntrinsicAttributes["category"]))
        );
    }
}
