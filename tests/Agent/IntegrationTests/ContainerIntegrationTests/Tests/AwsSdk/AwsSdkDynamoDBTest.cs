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

    private readonly string _tableName = $"TestTable-{Guid.NewGuid()}";

    private readonly string _metricScope1 = "WebTransaction/MVC/AwsSdkDynamoDB/CreateTable/{tableName}";
    private readonly string _metricScope2 = "WebTransaction/MVC/AwsSdkDynamoDB/PutItem/{tableName}/{title}/{year}";
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

                _fixture.CreateTableAsync(_tableName);
                _fixture.PutItemAsync(_tableName, "Ghost", 1990);

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

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/create_table", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/create_table", callCount = 1, metricScope = _metricScope1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/describe_table", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/describe_table", callCount = 1, metricScope = _metricScope1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/put_item", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/put_item", callCount = 1, metricScope = _metricScope2},

        };

        Assertions.MetricsExist(expectedMetrics, metrics);
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

