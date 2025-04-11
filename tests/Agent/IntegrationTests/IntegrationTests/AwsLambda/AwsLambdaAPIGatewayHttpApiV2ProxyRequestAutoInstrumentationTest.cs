// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.AwsLambda.AutoInstrumentation;

public abstract class AwsLambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTest<T> : NewRelicIntegrationTest<T> where T : AspNetCoreWebApiLambdaFixtureBase
{
    private readonly T _fixture;
    private readonly object _expectedTransactionName;

    protected AwsLambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTest(T fixture, ITestOutputHelper output, string expectedTransactionName) : base(fixture)
    {
        _fixture = fixture;
        _expectedTransactionName = expectedTransactionName;
        _fixture.TestLogger = output;
        _fixture.SetAdditionalEnvironmentVariable("NEW_RELIC_ATTRIBUTES_INCLUDE", "request.headers.*,request.parameters.*");
        _fixture.Actions(
            exerciseApplication: () =>
            {
                _fixture.EnqueueAPIGatewayHttpApiV2ProxyRequest();
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.ServerlessPayloadLogLineRegex, TimeSpan.FromMinutes(1), 1);
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var serverlessPayloads = _fixture.AgentLog.GetServerlessPayloads().ToList();

        Assert.Multiple(
                () => Assert.Single(serverlessPayloads),
                () => ValidateServerlessPayload(serverlessPayloads[0])
            );
    }

    private void ValidateServerlessPayload(ServerlessPayload serverlessPayload)
    {
        var transactionEvent = serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single();

        var expectedAgentAttributes = new[]
        {
            "aws.lambda.arn",
            "aws.requestId",
            "host.displayName"
        };

        var expectedAgentAttributeValues = new Dictionary<string, object>
        {
            { "aws.lambda.eventSource.accountId", "123456789012" },
            { "aws.lambda.eventSource.apiId", "api-id" },
            { "aws.lambda.eventSource.eventType", "apiGateway" },
            { "aws.lambda.eventSource.stage", "$default" },
            { "request.headers.header1", "value1" },
            { "request.headers.header2", "value1,value2" },
            { "request.method", "GET" },
            { "request.uri", "/api/values" },
            { "http.statusCode", 200 },
            { "response.status", "200" },
        };

        Assert.Equal(_expectedTransactionName, transactionEvent.IntrinsicAttributes["name"]);

        Assertions.TransactionEventHasAttributes(expectedAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent);
        Assertions.TransactionEventHasAttributes(expectedAgentAttributeValues, TransactionEventAttributeType.Agent, transactionEvent);
    }
}

public class AwsLambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTestCoreOldest : AwsLambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTest<LambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTriggerFixtureCoreOldest>
{
    public AwsLambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTestCoreOldest(LambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, "WebTransaction/MVC/Values/Get")
    {
    }
}
