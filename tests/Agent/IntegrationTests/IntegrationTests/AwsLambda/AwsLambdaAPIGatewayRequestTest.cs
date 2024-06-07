// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.WebRequest
{
    [NetCoreTest]
    public abstract class AwsLambdaAPIGatewayProxyRequestTest<T> : NewRelicIntegrationTest<T> where T : LambdaAPIGatewayProxyRequestTriggerFixtureBase
    {
        private readonly LambdaAPIGatewayProxyRequestTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;
        private readonly bool _returnsStream;
        private const string TestTraceId = "74be672b84ddc4e4b28be285632bbc0a";
        private const string TestParentSpanId = "27ddd2d8890283b4";

        protected AwsLambdaAPIGatewayProxyRequestTest(T fixture, ITestOutputHelper output, string expectedTransactionName, bool returnsStream)
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
                    _fixture.EnqueueAPIGatewayProxyRequest();
                    _fixture.EnqueueAPIGatewayProxyRequestWithDTHeaders(TestTraceId, TestParentSpanId);
                    _fixture.EnqueueMinimalAPIGatewayProxyRequest();
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
                () => Assert.Equal(3, serverlessPayloads.Count),
                // validate the first 2 payloads separately from the 3rd
                () => Assert.All(serverlessPayloads.GetRange(0, 2), ValidateServerlessPayload),
                () => ValidateMinimalRequestPayload(serverlessPayloads[2]),
                () => ValidateTraceHasNoParent(serverlessPayloads[0]),
                () => ValidateTraceHasParent(serverlessPayloads[1])
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
                {"request.method", "POST" },
                {"request.uri", "/path/to/resource" },
                {"request.parameters.foo", "bar" }
            };

            if (!_returnsStream) // stream response type won't have response attributes
            {
                expectedAgentAttributeValues.Add("http.statusCode", 200);
                expectedAgentAttributeValues.Add("response.status", "200");
                expectedAgentAttributeValues.Add("response.headers.content-type", "application/json");
                expectedAgentAttributeValues.Add("response.headers.content-length", "12345");
            }

            Assert.Equal(_expectedTransactionName, transactionEvent.IntrinsicAttributes["name"]);

            Assertions.TransactionEventHasAttributes(expectedAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionEventHasAttributes(expectedAgentAttributeValues, TransactionEventAttributeType.Agent, transactionEvent);

            if (_returnsStream) // verify stream response type does not have response attributes
            {
                var unexpectedAgentAttributeValues = new[]
                    { "http.statusCode", "response.status", "response.headers.content-type", "response.headers.content-length" };
                Assertions.TransactionEventDoesNotHaveAttributes(unexpectedAgentAttributeValues, TransactionEventAttributeType.Agent, transactionEvent);
            }
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
            };

            if (!_returnsStream) // stream response type won't have response attributes
            {
                expectedAgentAttributeValues.Add("http.statusCode", 200);
                expectedAgentAttributeValues.Add("response.status", "200");
                expectedAgentAttributeValues.Add("response.headers.content-type", "application/json");
                expectedAgentAttributeValues.Add("response.headers.content-length", "12345");
            }

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

    public class AwsLambdaAPIGatewayProxyRequestTestNet6 : AwsLambdaAPIGatewayProxyRequestTest<LambdaAPIGatewayProxyRequestTriggerFixtureNet6>
    {
        public AwsLambdaAPIGatewayProxyRequestTestNet6(LambdaAPIGatewayProxyRequestTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayProxyRequestHandler", false)
        {
        }
    }

    public class AwsLambdaAPIGatewayProxyRequestTestNet8 : AwsLambdaAPIGatewayProxyRequestTest<LambdaAPIGatewayProxyRequestTriggerFixtureNet8>
    {
        public AwsLambdaAPIGatewayProxyRequestTestNet8(LambdaAPIGatewayProxyRequestTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayProxyRequestHandler", false)
        {
        }
    }
    public class AwsLambdaAPIGatewayProxyRequestTestAsyncNet6 : AwsLambdaAPIGatewayProxyRequestTest<AsyncLambdaAPIGatewayProxyRequestTriggerFixtureNet6>
    {
        public AwsLambdaAPIGatewayProxyRequestTestAsyncNet6(AsyncLambdaAPIGatewayProxyRequestTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayProxyRequestHandlerAsync", false)
        {
        }
    }

    public class AwsLambdaAPIGatewayProxyRequestTestAsyncNet8 : AwsLambdaAPIGatewayProxyRequestTest<AsyncLambdaAPIGatewayProxyRequestTriggerFixtureNet8>
    {
        public AwsLambdaAPIGatewayProxyRequestTestAsyncNet8(AsyncLambdaAPIGatewayProxyRequestTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayProxyRequestHandlerAsync", false)
        {
        }
    }

    public class AwsLambdaAPIGatewayProxyRequestReturnsStreamTestNet6 : AwsLambdaAPIGatewayProxyRequestTest<LambdaAPIGatewayProxyRequestReturnsStreamTriggerFixtureNet6>
    {
        public AwsLambdaAPIGatewayProxyRequestReturnsStreamTestNet6(LambdaAPIGatewayProxyRequestReturnsStreamTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayProxyRequestHandlerReturnsStream", true)
        {
        }
    }

    public class AwsLambdaAPIGatewayProxyRequestReturnsStreamTestNet8 : AwsLambdaAPIGatewayProxyRequestTest<LambdaAPIGatewayProxyRequestReturnsStreamTriggerFixtureNet8>
    {
        public AwsLambdaAPIGatewayProxyRequestReturnsStreamTestNet8(LambdaAPIGatewayProxyRequestReturnsStreamTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayProxyRequestHandlerReturnsStream", true)
        {
        }
    }
    public class AwsLambdaAPIGatewayProxyRequestReturnsStreamTestAsyncNet6 : AwsLambdaAPIGatewayProxyRequestTest<AsyncLambdaAPIGatewayProxyRequestReturnsStreamTriggerFixtureNet6>
    {
        public AwsLambdaAPIGatewayProxyRequestReturnsStreamTestAsyncNet6(AsyncLambdaAPIGatewayProxyRequestReturnsStreamTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayProxyRequestHandlerReturnsStreamAsync", true)
        {
        }
    }

    public class AwsLambdaAPIGatewayProxyRequestReturnsStreamTestAsyncNet8 : AwsLambdaAPIGatewayProxyRequestTest<AsyncLambdaAPIGatewayProxyRequestReturnsStreamTriggerFixtureNet8>
    {
        public AwsLambdaAPIGatewayProxyRequestReturnsStreamTestAsyncNet8(AsyncLambdaAPIGatewayProxyRequestReturnsStreamTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApiGatewayProxyRequestHandlerReturnsStreamAsync", true)
        {
        }
    }
}
