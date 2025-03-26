// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.AwsLambda.WebRequest
{
    [NetCoreTest]
    public abstract class AwsLambdaAPIGatewayHttpApiV2ProxyRequestTest<T> : NewRelicIntegrationTest<T> where T : LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureBase
    {
        private readonly LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;
        private readonly bool _returnsStream;
        private const string TestTraceId = "74be672b84ddc4e4b28be285632bbc0a";
        private const string TestParentSpanId = "27ddd2d8890283b4";

        protected AwsLambdaAPIGatewayHttpApiV2ProxyRequestTest(T fixture, ITestOutputHelper output, string expectedTransactionName, bool returnsStream)
            : base(fixture)
        {
            _fixture = fixture;
            _expectedTransactionName = expectedTransactionName;
            _returnsStream = returnsStream;
            _fixture.TestLogger = output;
            _fixture.SetAdditionalEnvironmentVariable("NEW_RELIC_ATTRIBUTES_INCLUDE", "request.headers.*,request.parameters.*");
            _fixture.Actions(
                exerciseApplication: () =>
                {
                    _fixture.EnqueueAPIGatewayHttpApiV2ProxyRequest();
                    _fixture.EnqueueAPIGatewayHttpApiV2ProxyRequestWithDTHeaders(TestTraceId, TestParentSpanId);
                    _fixture.EnqueueMinimalAPIGatewayHttpApiV2ProxyRequest();
                    _fixture.EnqueueInvalidAPIGatewayHttpApiV2ProxyRequest();

                    // wait for the invalid request log line
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.InvalidServerlessWebRequestLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.ServerlessPayloadLogLineRegex, TimeSpan.FromMinutes(1), 3);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var serverlessPayloads = _fixture.AgentLog.GetServerlessPayloads().ToList();

            Assert.Multiple(
                // the fourth exerciser invocation should result in a NoOpDelegate, so there will only be 3 payloads
                () => Assert.Equal(3, serverlessPayloads.Count),
                // validate the first 2 payloads separately from the 3rd
                () => Assert.All(serverlessPayloads.GetRange(0, 2), ValidateServerlessPayload),
                () => ValidateMinimalRequestPayload(serverlessPayloads[2]),
                () => ValidateTraceHasNoParent(serverlessPayloads[0]),
                () => ValidateTraceHasParent(serverlessPayloads[1])
                );

            // verify that the invalid request payload generated the expected log line
            var logLines = _fixture.AgentLog.TryGetLogLines(AgentLogBase.InvalidServerlessWebRequestLogLineRegex);
            Assert.Single(logLines);
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
                { "request.method", "POST" },
                { "request.uri", "/path/to/resource" },
                { "request.parameters.parameter1", "value1,value2" },
                { "request.parameters.parameter2", "value" },
                { "http.statusCode", 200 },
                { "response.status", "200" },
                { "response.headers.content-type", "application/json" },
                { "response.headers.content-length", "12345" }
            };

            Assert.Equal(_expectedTransactionName, transactionEvent.IntrinsicAttributes["name"]);

            Assertions.TransactionEventHasAttributes(expectedAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionEventHasAttributes(expectedAgentAttributeValues, TransactionEventAttributeType.Agent, transactionEvent);
        }

        private void ValidateMinimalRequestPayload(ServerlessPayload serverlessPayload)
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
                { "aws.lambda.eventSource.eventType", "apiGateway" },
                {"request.method", "POST" },
                {"request.uri", "/path/to/resource" },
                { "http.statusCode", 200 },
                { "response.status", "200" },
                { "response.headers.content-type", "application/json" },
                { "response.headers.content-length", "12345" }
            };

            Assert.Equal(_expectedTransactionName, transactionEvent.IntrinsicAttributes["name"]);

            Assertions.TransactionEventHasAttributes(expectedAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionEventHasAttributes(expectedAgentAttributeValues, TransactionEventAttributeType.Agent, transactionEvent);
        }

        private void ValidateTraceHasNoParent(ServerlessPayload serverlessPayload)
        {
            var entrySpan = serverlessPayload.Telemetry.SpanEventsPayload.SpanEvents.Single(s => (string)s.IntrinsicAttributes["name"] == _expectedTransactionName);

            Assertions.SpanEventDoesNotHaveAttributes(["parentId"], SpanEventAttributeType.Intrinsic, entrySpan);
        }

        private void ValidateTraceHasParent(ServerlessPayload serverlessPayload)
        {
            var entrySpan = serverlessPayload.Telemetry.SpanEventsPayload.SpanEvents.Single(s => (string)s.IntrinsicAttributes["name"] == _expectedTransactionName);

            var expectedAttributeValues = new Dictionary<string, object>
            {
                { "traceId", TestTraceId },
                { "parentId", TestParentSpanId }
            };

            Assertions.SpanEventHasAttributes(expectedAttributeValues, SpanEventAttributeType.Intrinsic, entrySpan);
        }
    }

    public class AwsLambdaAPIGatewayHttpApiV2ProxyRequestTestCoreOldest : AwsLambdaAPIGatewayHttpApiV2ProxyRequestTest<LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureCoreOldest>
    {
        public AwsLambdaAPIGatewayHttpApiV2ProxyRequestTestCoreOldest(LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayHttpApiV2ProxyRequestHandler", false)
        {
        }
    }

    public class AwsLambdaAPIGatewayHttpApiV2ProxyRequestTestCoreLatest : AwsLambdaAPIGatewayHttpApiV2ProxyRequestTest<LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureCoreLatest>
    {
        public AwsLambdaAPIGatewayHttpApiV2ProxyRequestTestCoreLatest(LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayHttpApiV2ProxyRequestHandler", false)
        {
        }
    }

    public class AwsLambdaAPIGatewayHttpApiV2ProxyRequestTestAsyncCoreOldest : AwsLambdaAPIGatewayHttpApiV2ProxyRequestTest<AsyncLambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureCoreOldest>
    {
        public AwsLambdaAPIGatewayHttpApiV2ProxyRequestTestAsyncCoreOldest(AsyncLambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayHttpApiV2ProxyRequestHandlerAsync", false)
        {
        }
    }

    public class AwsLambdaAPIGatewayHttpApiV2ProxyRequestTestAsyncCoreLatest : AwsLambdaAPIGatewayHttpApiV2ProxyRequestTest<AsyncLambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureCoreLatest>
    {
        public AwsLambdaAPIGatewayHttpApiV2ProxyRequestTestAsyncCoreLatest(AsyncLambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayHttpApiV2ProxyRequestHandlerAsync", false)
        {
        }
    }
}
