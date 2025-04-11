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

public abstract class AwsLambdaApplicationLoadBalancerRequestAutoInstrumentationTest<T> : NewRelicIntegrationTest<T> where T : AspNetCoreWebApiLambdaFixtureBase
{
    private readonly T _fixture;
    private readonly object _expectedTransactionName;

    protected AwsLambdaApplicationLoadBalancerRequestAutoInstrumentationTest(T fixture, ITestOutputHelper output, string expectedTransactionName) : base(fixture)
    {
        _fixture = fixture;
        _expectedTransactionName = expectedTransactionName;
        _fixture.TestLogger = output;
        _fixture.SetAdditionalEnvironmentVariable("NEW_RELIC_ATTRIBUTES_INCLUDE", "request.headers.*,request.parameters.*");
        _fixture.Actions(
            exerciseApplication: () =>
            {
                _fixture.EnqueueApplicationLoadBalancerRequest();
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
            { "aws.lambda.eventSource.eventType", "alb" },
            { "aws.lambda.eventSource.arn", "arn:aws:elasticloadbalancing:us-east-2:123456789012:targetgroup/lambda-279XGJDqGZ5rsrHC2Fjr/49e9d65c45c6791a"},
            { "request.headers.accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8" },
            { "request.headers.accept-encoding", "gzip" },
            { "request.headers.accept-language", "en-US,en;q=0.9" },
            { "request.headers.connection", "keep-alive" },
            { "request.headers.host", "lambda-alb-123578498.us-east-2.elb.amazonaws.com" },
            { "request.headers.upgrade-insecure-requests", "1" },
            { "request.headers.user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Safari/537.36" },
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

public class AwsLambdaApplicationLoadBalancerRequestAutoInstrumentationTestTestCoreOldest : AwsLambdaApplicationLoadBalancerRequestAutoInstrumentationTest<LambdaApplicationLoadBalancerRequestAutoInstrumentationTriggerFixtureCoreOldest>
{
    public AwsLambdaApplicationLoadBalancerRequestAutoInstrumentationTestTestCoreOldest(LambdaApplicationLoadBalancerRequestAutoInstrumentationTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, "WebTransaction/MVC/Values/Get")
    {
    }
}
