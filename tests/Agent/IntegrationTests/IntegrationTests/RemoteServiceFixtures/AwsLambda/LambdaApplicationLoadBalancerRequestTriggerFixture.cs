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

        /// <summary>
        /// An invalid payload to validate the fix for https://github.com/newrelic/newrelic-dotnet-agent/issues/2652
        /// </summary>
        public void EnqueueInvalidLoadBalancerRequestyRequest()
        {
            var invalidLoadBalancerRequestJson = $$"""
                                                      {
                                                        "foo": "bar"
                                                      }
                                                      """;
            EnqueueLambdaEvent(invalidLoadBalancerRequestJson);
        }
    }

    public class LambdaApplicationLoadBalancerRequestTriggerFixtureCoreOldest : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestTriggerFixtureCoreOldest() : base(CoreOldestTFM, false, false) { }
    }

    public class AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureCoreOldest : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureCoreOldest() : base(CoreOldestTFM, true, false) { }
    }

    public class LambdaApplicationLoadBalancerRequestTriggerFixtureCoreLatest : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestTriggerFixtureCoreLatest() : base(CoreLatestTFM, false, false) { }
    }

    public class AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureCoreLatest : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public AsyncLambdaApplicationLoadBalancerRequestTriggerFixtureCoreLatest() : base(CoreLatestTFM, true, false) { }
    }

    public class LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureCoreOldest : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureCoreOldest() : base(CoreOldestTFM, false, true) { }
    }

    public class AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureCoreOldest : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureCoreOldest() : base(CoreOldestTFM, true, true) { }
    }

    public class LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureCoreLatest : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureCoreLatest() : base(CoreLatestTFM, false, true) { }
    }

    public class AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureCoreLatest : LambdaApplicationLoadBalancerRequestTriggerFixtureBase
    {
        public AsyncLambdaApplicationLoadBalancerRequestReturnsStreamTriggerFixtureCoreLatest() : base(CoreLatestTFM, true, true) { }
    }
}
