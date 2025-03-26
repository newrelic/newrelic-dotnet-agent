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

[NetCoreTest]
public abstract class AwsLambdaAPIGatewayRequestAutoInstrumentationTest<T> : NewRelicIntegrationTest<T> where T : AspNetCoreWebApiLambdaFixtureBase
{
    private readonly T _fixture;
    private readonly object _expectedTransactionName;

    protected AwsLambdaAPIGatewayRequestAutoInstrumentationTest(T fixture, ITestOutputHelper output, string expectedTransactionName) : base(fixture)
    {
        _fixture = fixture;
        _expectedTransactionName = expectedTransactionName;
        _fixture.TestLogger = output;
        _fixture.SetAdditionalEnvironmentVariable("NEW_RELIC_ATTRIBUTES_INCLUDE", "request.headers.*,request.parameters.*");
        _fixture.Actions(
            exerciseApplication: () =>
            {
                _fixture.EnqueueAPIGatewayProxyRequest();
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
                { "aws.lambda.eventSource.apiId", "1234567890" },
                { "aws.lambda.eventSource.eventType", "apiGateway" },
                { "aws.lambda.eventSource.resourceId", "123456" },
                { "aws.lambda.eventSource.resourcePath", "/{proxy+}" },
                { "aws.lambda.eventSource.stage", "prod" },
                {"request.headers.accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8" },
                {"request.headers.accept-encoding", "gzip, deflate, sdch" },
                {"request.headers.accept-language", "en-US,en;q=0.8" },
                {"request.headers.cache-control", "max-age=0" },
                {"request.headers.cloudfront-forwarded-proto", "https" },
                {"request.headers.cloudfront-is-desktop-viewer", "true" },
                {"request.headers.cloudfront-is-mobile-viewer", "false" },
                {"request.headers.cloudfront-is-smarttv-viewer", "false" },
                {"request.headers.cloudfront-is-tablet-viewer", "false" },
                {"request.headers.cloudfront-viewer-country", "US" },
                {"request.headers.host", "1234567890.execute-api.{dns_suffix}" },
                {"request.headers.upgrade-insecure-requests", "1" },
                {"request.headers.user-agent", "Custom User Agent String" },
                {"request.headers.via", "1.1 08f323deadbeefa7af34d5feb414ce27.cloudfront.net (CloudFront)" },
                {"request.method", "GET" },
                {"request.uri", "/api/values" },
                { "http.statusCode", 200 },
                { "response.status", "200" },
            };

            Assert.Equal(_expectedTransactionName, transactionEvent.IntrinsicAttributes["name"]);

            Assertions.TransactionEventHasAttributes(expectedAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionEventHasAttributes(expectedAgentAttributeValues, TransactionEventAttributeType.Agent, transactionEvent);
    }
}

public class AwsLambdaAPIGatewayRequestAutoInstrumentationTestTestCoreOldest : AwsLambdaAPIGatewayRequestAutoInstrumentationTest<LambdaAPIGatewayProxyRequestAutoInstrumentationTriggerFixtureCoreOldest>
{
    public AwsLambdaAPIGatewayRequestAutoInstrumentationTestTestCoreOldest(LambdaAPIGatewayProxyRequestAutoInstrumentationTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, "WebTransaction/MVC/Values/Get")
    {
    }
}
