// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests.AwsSdk;

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
                configModifier.LogToConsole();

            },
            exerciseApplication: () =>
            {
                _fixture.Delay(5);

                _fixture.ExerciseMultiService(_tableName, _queueName, _bookName);

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex,
                    TimeSpan.FromMinutes(2));

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        // TODO: Verify that cloud.resource_id appears only on the dynamodb datastore segment and not on the whole transaction.
        // TODO: Verify that the sqs message broker segment does not have cloud.resource_id.
        // TODO: verify that the account ID in cloud.resource_id matches the expected account id
    }
}
