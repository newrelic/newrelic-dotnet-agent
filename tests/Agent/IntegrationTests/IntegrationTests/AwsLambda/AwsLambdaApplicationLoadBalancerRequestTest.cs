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
    public abstract class AwsLambdaApplicationLoadBalancerRequestTest<T> : NewRelicIntegrationTest<T> where T : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        private readonly LambdaApplicationLoadBalancerRequestTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;
        private readonly bool _returnsStream;
        private const string TestTraceId = "74be672b84ddc4e4b28be285632bbc0a";
        private const string TestParentSpanId = "27ddd2d8890283b4";

        protected AwsLambdaApplicationLoadBalancerRequestTest(T fixture, ITestOutputHelper output, string expectedTransactionName, bool returnsStream)
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
                    _fixture.EnqueueApplicationLoadBalancerRequest();
                    _fixture.EnqueueApplicationLoadBalancerRequestWithDTHeaders(TestTraceId, TestParentSpanId);
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.ServerlessPayloadLogLineRegex, TimeSpan.FromMinutes(1), 2);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var serverlessPayloads = _fixture.AgentLog.GetServerlessPayloads().ToList();

            Assert.Multiple(
                () => Assert.Equal(2, serverlessPayloads.Count),
                () => Assert.All(serverlessPayloads, ValidateServerlessPayload),
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
                { "request.uri", "/path/to/resource" },
                { "request.parameters.foo", "bar" }
            };


            if (!_returnsStream) // stream response type won't have response attributes
            {
                expectedAgentAttributeValues.Add("http.statusCode", 200);
                expectedAgentAttributeValues.Add("response.status", "200");
                expectedAgentAttributeValues.Add("response.headers.content-type", "application/json");
                expectedAgentAttributeValues.Add("response.headers.content-length", "12345");
            };

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

    public class AwsLambdaApplicationLoadBalancerRequestTestNet6 : AwsLambdaApplicationLoadBalancerRequestTest<LambdaApplicationLoadBalancerRequestTriggerFixtureNet6>
    {
        public AwsLambdaApplicationLoadBalancerRequestTestNet6(LambdaApplicationLoadBalancerRequestTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApplicationLoadBalancerRequestHandler", false)
        {
        }
    }

    public class AwsLambdaApplicationLoadBalancerRequestTestNet8 : AwsLambdaApplicationLoadBalancerRequestTest<LambdaApplicationLoadBalancerRequestTriggerFixtureNet8>
    {
        public AwsLambdaApplicationLoadBalancerRequestTestNet8(LambdaApplicationLoadBalancerRequestTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApplicationLoadBalancerRequestHandler", false)
        {
        }
    }
    public class AwsLambdaApplicationLoadBalancerRequestAsyncTestNet6 : AwsLambdaApplicationLoadBalancerRequestTest<AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureNet6>
    {
        public AwsLambdaApplicationLoadBalancerRequestAsyncTestNet6(AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApplicationLoadBalancerRequestHandlerAsync", false)
        {
        }
    }

    public class AwsLambdaApplicationLoadBalancerRequestAsyncTestNet8 : AwsLambdaApplicationLoadBalancerRequestTest<AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureNet8>
    {
        public AwsLambdaApplicationLoadBalancerRequestAsyncTestNet8(AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApplicationLoadBalancerRequestHandlerAsync", false)
        {
        }
    }

    public class AwsLambdaApplicationLoadBalancerRequestReturnsStreamTestNet6 : AwsLambdaApplicationLoadBalancerRequestTest<LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet6>
    {
        public AwsLambdaApplicationLoadBalancerRequestReturnsStreamTestNet6(LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApplicationLoadBalancerRequestHandlerReturnsStream", true)
        {
        }
    }

    public class AwsLambdaApplicationLoadBalancerRequestReturnsStreamTestNet8 : AwsLambdaApplicationLoadBalancerRequestTest<LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet8>
    {
        public AwsLambdaApplicationLoadBalancerRequestReturnsStreamTestNet8(LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApplicationLoadBalancerRequestHandlerReturnsStream", true)
        {
        }
    }
    public class AwsLambdaApplicationLoadBalancerRequestReturnsStreamAsyncTestNet6 : AwsLambdaApplicationLoadBalancerRequestTest<AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet6>
    {
        public AwsLambdaApplicationLoadBalancerRequestReturnsStreamAsyncTestNet6(AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApplicationLoadBalancerRequestHandlerReturnsStreamAsync", true)
        {
        }
    }

    public class AwsLambdaApplicationLoadBalancerRequestReturnsStreamAsyncTestNet8 : AwsLambdaApplicationLoadBalancerRequestTest<AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet8>
    {
        public AwsLambdaApplicationLoadBalancerRequestReturnsStreamAsyncTestNet8(AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/Lambda/ApplicationLoadBalancerRequestHandlerReturnsStreamAsync", true)
        {
        }
    }
}
