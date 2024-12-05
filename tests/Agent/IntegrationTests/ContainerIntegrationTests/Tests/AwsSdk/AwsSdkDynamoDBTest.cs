// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests.AwsSdk;

public abstract class AwsSdkDynamoDBTestBase : NewRelicIntegrationTest<AwsSdkContainerDynamoDBTestFixture>
{
    private readonly AwsSdkContainerDynamoDBTestFixture _fixture;

    private readonly string _tableName = $"TestTable-{Guid.NewGuid()}";
    private readonly string _title = "Ghost";
    private readonly string _year = "1990";

    private const string _accountId = "520056171328"; // matches the account ID parsed from the fake access key used in AwsSdkDynamoDBExerciser

    protected AwsSdkDynamoDBTestBase(AwsSdkContainerDynamoDBTestFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.SetAdditionalEnvironmentVariable("AWSSDK_INITCOLLECTIONS", "true");

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

                _fixture.PutItemAsync(_tableName, _title, _year);
                _fixture.GetItemAsync(_tableName, _title, _year);
                _fixture.UpdateItemAsync(_tableName, _title, _year);

                _fixture.QueryAsync(_tableName, _title, _year);
                _fixture.ScanAsync(_tableName);

                _fixture.DeleteItemAsync(_tableName, _title, _year);
                _fixture.DeleteTableAsync(_tableName);

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2));

                //// shut down the container and wait for the agent log to see it
                //_fixture.ShutdownRemoteApplication();
                //_fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }


    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var metricScopeBase = "WebTransaction/MVC/AwsSdkDynamoDB/";
        var createTableScope = metricScopeBase + "CreateTable/{tableName}";
        var scanScope = metricScopeBase + "Scan/{tableName}";
        var deleteTableScope = metricScopeBase + "DeleteTable/{tableName}";
        var putItemScope = metricScopeBase + "PutItem/{tableName}/{title}/{year}";
        var getItemScope = metricScopeBase + "GetItem/{tableName}/{title}/{year}";
        var updateItemScope = metricScopeBase + "UpdateItem/{tableName}/{title}/{year}";
        var deleteItemScope = metricScopeBase + "DeleteItem/{tableName}/{title}/{year}";
        var queryScope = metricScopeBase + "Query/{tableName}/{title}/{year}";

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/create_table", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/create_table", callCount = 1, metricScope = createTableScope},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/describe_table", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/describe_table", callCount = 1, metricScope = createTableScope},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/put_item", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/put_item", callCount = 1, metricScope = putItemScope},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/get_item", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/get_item", callCount = 1, metricScope = getItemScope},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/update_item", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/update_item", callCount = 1, metricScope = updateItemScope},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/delete_item", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/delete_item", callCount = 1, metricScope = deleteItemScope},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/query", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/query", callCount = 1, metricScope = queryScope},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/scan", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/scan", callCount = 1, metricScope = scanScope},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/delete_table", callCount = 1},
            new() { metricName = $"Datastore/statement/DynamoDB/{_tableName}/delete_table", callCount = 1, metricScope = deleteTableScope},

        };

        var expectedAttributes = new Dictionary<string, object> {{ "cloud.resource_id", $"arn:aws:dynamodb:(unknown):{_accountId}:table/{_tableName}" } };

        var transactionSample = _fixture.AgentLog.TryGetTransactionSample(createTableScope);
        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(createTableScope);
        var spanEvent = _fixture.AgentLog.TryGetSpanEvent(createTableScope);


        Assert.Multiple(
            () => Assert.Equal(0, _fixture.AgentLog.GetWrapperExceptionLineCount()),
            () => Assert.Equal(0, _fixture.AgentLog.GetApplicationErrorLineCount()),

            () => Assert.NotNull(transactionSample),
            () => Assert.NotNull(transactionEvent),
            () => Assert.NotNull(spanEvent),

            () => Assertions.TransactionTraceHasAttributes(expectedAttributes, Agent.Tests.TestSerializationHelpers.Models.TransactionTraceAttributeType.Agent, transactionSample),
            () => Assertions.TransactionEventDoesNotHaveAttributes(["cloud.resource_id"], Agent.Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Agent, transactionEvent),
            () => Assertions.SpanEventHasAttributes(expectedAttributes, Agent.Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, spanEvent),

            () => Assertions.MetricsExist(expectedMetrics, metrics)
            );




    }
}

// Base class with derived classes pattern copied from another tests file
// but we currently don't need to use it for anything

public class AwsSdkDynamoDBTest : AwsSdkDynamoDBTestBase
{
    public AwsSdkDynamoDBTest(AwsSdkContainerDynamoDBTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

