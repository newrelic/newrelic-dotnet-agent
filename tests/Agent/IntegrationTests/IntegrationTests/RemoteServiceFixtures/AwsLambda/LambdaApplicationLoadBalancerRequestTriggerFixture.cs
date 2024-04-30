// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaApplicationLoadBalancerRequestTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        private static string GetHandlerString(bool isAsync, bool returnsStream)
        {
            return "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::ApplicationLoadBalancerRequestHandler" + (returnsStream ? "ReturnsStream" : "") + (isAsync ? "Async" : "");
        }

        protected LambdaApplicationLoadBalancerRequestTriggerFixtureBase(string targetFramework, bool isAsync, bool returnsStream) :
            base(targetFramework,
                null,
                GetHandlerString(isAsync, returnsStream),
                "ApplicationLoadBalancerRequestHandler" + (returnsStream ? "ReturnsStream" : "") + (isAsync ? "Async" : ""),
                null)
        {
        }

        public void EnqueueApplicationLoadBalancerRequest()
        {
            var ApplicationLoadBalancerRequestJson = $$"""
                                               {
                                                 "requestContext": {
                                                   "elb": {
                                                     "targetGroupArn": "arn:aws:elasticloadbalancing:us-east-2:123456789012:targetgroup/lambda-279XGJDqGZ5rsrHC2Fjr/49e9d65c45c6791a"
                                                   }
                                                 },
                                                 "httpMethod": "GET",
                                                 "path": "/path/to/resource",
                                                 "queryStringParameters": {
                                                   "foo": "bar"
                                                 },
                                                 "headers": {
                                                   "accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8",
                                                   "accept-encoding": "gzip",
                                                   "accept-language": "en-US,en;q=0.9",
                                                   "connection": "keep-alive",
                                                   "host": "lambda-alb-123578498.us-east-2.elb.amazonaws.com",
                                                   "upgrade-insecure-requests": "1",
                                                   "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Safari/537.36",
                                                   "x-amzn-trace-id": "Root=1-5c536348-3d683b8b04734faae651f476",
                                                   "x-forwarded-for": "72.12.164.125",
                                                   "x-forwarded-port": "443",
                                                   "x-forwarded-proto": "https",
                                                   "x-imforwards": "20"
                                                 },
                                                 "body": "request_body",
                                                 "isBase64Encoded": false
                                               }
                                               """;
            EnqueueLambdaEvent(ApplicationLoadBalancerRequestJson);
        }

        public void EnqueueApplicationLoadBalancerRequestWithDTHeaders(string traceId, string spanId)
        {
            var ApplicationLoadBalancerRequestJson = $$"""
                                               {
                                                 "requestContext": {
                                                   "elb": {
                                                     "targetGroupArn": "arn:aws:elasticloadbalancing:us-east-2:123456789012:targetgroup/lambda-279XGJDqGZ5rsrHC2Fjr/49e9d65c45c6791a"
                                                   }
                                                 },
                                                 "httpMethod": "GET",
                                                 "path": "/path/to/resource",
                                                 "queryStringParameters": {
                                                   "foo": "bar"
                                                 },
                                                 "headers": {
                                                   "accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8",
                                                   "accept-encoding": "gzip",
                                                   "accept-language": "en-US,en;q=0.9",
                                                   "connection": "keep-alive",
                                                   "host": "lambda-alb-123578498.us-east-2.elb.amazonaws.com",
                                                   "upgrade-insecure-requests": "1",
                                                   "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Safari/537.36",
                                                   "x-amzn-trace-id": "Root=1-5c536348-3d683b8b04734faae651f476",
                                                   "x-forwarded-for": "72.12.164.125",
                                                   "x-forwarded-port": "443",
                                                   "x-forwarded-proto": "https",
                                                   "x-imforwards": "20",
                                                   "traceparent": "{{GetTestTraceParentHeaderValue(traceId, spanId)}}",
                                                   "tracestate": "{{GetTestTraceStateHeaderValue(spanId)}}"
                                                 },
                                                 "body": "request_body",
                                                 "isBase64Encoded": false
                                               }
                                               """;
            EnqueueLambdaEvent(ApplicationLoadBalancerRequestJson);
        }
    }

    public class LambdaApplicationLoadBalancerRequestTriggerFixtureNet6 : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestTriggerFixtureNet6() : base("net6.0", false, false) { }
    }

    public class AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureNet6 : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureNet6() : base("net6.0", true, false) { }
    }

    public class LambdaApplicationLoadBalancerRequestTriggerFixtureNet8 : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestTriggerFixtureNet8() : base("net8.0", false, false) { }
    }

    public class AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureNet8 : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureNet8() : base("net8.0", true, false) { }
    }

    public class LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet6 : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet6() : base("net6.0", false, true) { }
    }

    public class AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet6 : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet6() : base("net6.0", true, true) { }
    }

    public class LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet8 : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet8() : base("net8.0", false, true) { }
    }

    public class AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet8 : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureNet8() : base("net8.0", true, true) { }
    }
}
