// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class AspNetCoreWebApiLambdaFixtureBase : LambdaTestToolFixture
    {
        protected AspNetCoreWebApiLambdaFixtureBase(string targetFramework) :
            base(new RemoteService("AspNetCoreWebApiLambdaApplication", "AspNetCoreWebApiLambdaApplication.exe", targetFramework, ApplicationType.Bounded, createsPidFile: true, isCoreApp: true, publishApp: true),
                "",
                "AspNetCoreWebApiLambdaApplication::AspNetCoreWebApiLambdaApplication.LambdaEntryPoint::FunctionHandlerAsync",
                "AspNetCoreWebApiLambda",
                "latest",
                "aspnetcore",
                false)
        {

        }

        public void EnqueueAPIGatewayProxyRequest()
        {
            var apiGatewayProxyRequestJson = $$"""
                                               {
                                                 "path": "/api/values",
                                                 "httpMethod": "GET",
                                                 "headers": {
                                                   "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
                                                   "Accept-Encoding": "gzip, deflate, sdch",
                                                   "Accept-Language": "en-US,en;q=0.8",
                                                   "Cache-Control": "max-age=0",
                                                   "CloudFront-Forwarded-Proto": "https",
                                                   "CloudFront-Is-Desktop-Viewer": "true",
                                                   "CloudFront-Is-Mobile-Viewer": "false",
                                                   "CloudFront-Is-SmartTV-Viewer": "false",
                                                   "CloudFront-Is-Tablet-Viewer": "false",
                                                   "CloudFront-Viewer-Country": "US",
                                                   "Host": "1234567890.execute-api.{dns_suffix}",
                                                   "Upgrade-Insecure-Requests": "1",
                                                   "User-Agent": "Custom User Agent String",
                                                   "Via": "1.1 08f323deadbeefa7af34d5feb414ce27.cloudfront.net (CloudFront)",
                                                   "X-Amz-Cf-Id": "cDehVQoZnx43VYQb9j2-nvCh-9z396Uhbp027Y2JvkCPNLmGJHqlaA==",
                                                   "X-Forwarded-For": "127.0.0.1, 127.0.0.2",
                                                   "X-Forwarded-Port": "443",
                                                   "X-Forwarded-Proto": "https"
                                               },
                                                 "requestContext": {
                                                   "accountId": "123456789012",
                                                   "resourceId": "123456",
                                                   "stage": "prod",
                                                   "requestId": "c6af9ac6-7b61-11e6-9a41-93e8deadbeef",
                                                   "identity": {
                                                     "cognitoIdentityPoolId": null,
                                                     "accountId": null,
                                                     "cognitoIdentityId": null,
                                                     "caller": null,
                                                     "apiKey": null,
                                                     "sourceIp": "127.0.0.1",
                                                     "cognitoAuthenticationType": null,
                                                     "cognitoAuthenticationProvider": null,
                                                     "userArn": null,
                                                     "userAgent": "Custom User Agent String",
                                                     "user": null
                                                   },
                                                   "resourcePath": "/{proxy+}",
                                                   "httpMethod": "GET",
                                                   "apiId": "1234567890"
                                                 }
                                               }
                                               """;
            EnqueueLambdaEvent(apiGatewayProxyRequestJson);
        }

        public void EnqueueAPIGatewayHttpApiV2ProxyRequest()
        {
            var apiGatewayHttpApiV2ProxyRequestJson = $$$"""
                                               {
                                                   "Version": "2.0",
                                                   "RouteKey": "$default",
                                                   "RawPath": "/api/values",
                                                   "Headers": {
                                                       "Header1": "value1",
                                                       "Header2": "value1,value2"
                                                   },
                                                   "RequestContext": {
                                                       "AccountId": "123456789012",
                                                       "ApiId": "api-id",
                                                       "DomainName": "id.execute-api.us-east-1.amazonaws.com",
                                                       "DomainPrefix": "id",
                                                       "Http": {
                                                           "Method": "GET",
                                                           "Path": "/api/values",
                                                           "Protocol": "HTTP/1.1",
                                                           "SourceIp": "192.168.0.1/32",
                                                           "UserAgent": "agent"
                                                       },
                                                       "RequestId": "id",
                                                       "RouteKey": "$default",
                                                       "Stage": "$default",
                                                       "Time": "12/Mar/2020:19:03:58 +0000",
                                                       "TimeEpoch": 1583348638390
                                                   },
                                                   "StageVariables": {
                                                       "stageVariable1": "value1",
                                                       "stageVariable2": "value2"
                                                   }
                                               }
                                               """;

            EnqueueLambdaEvent(apiGatewayHttpApiV2ProxyRequestJson);
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
                                                 "path": "/api/values",
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
    }


    public class LambdaAPIGatewayProxyRequestAutoInstrumentationTriggerFixtureCoreOldest : AspNetCoreWebApiLambdaFixtureBase
    {
        public LambdaAPIGatewayProxyRequestAutoInstrumentationTriggerFixtureCoreOldest() : base(CoreOldestTFM)
        {
            CommandLineArguments = "--handler APIGatewayProxyFunctionEntryPoint";
        }
    }
    public class LambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTriggerFixtureCoreOldest : AspNetCoreWebApiLambdaFixtureBase
    {
        public LambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTriggerFixtureCoreOldest() : base(CoreOldestTFM)
        {
            CommandLineArguments = "--handler APIGatewayHttpApiV2ProxyFunctionEntryPoint";
        }
    }
    public class LambdaApplicationLoadBalancerRequestAutoInstrumentationTriggerFixtureCoreOldest : AspNetCoreWebApiLambdaFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestAutoInstrumentationTriggerFixtureCoreOldest() : base(CoreOldestTFM)
        {
            CommandLineArguments = "--handler ApplicationLoadBalancerFunctionEntryPoint";
        }
    }
    public class LambdaAPIGatewayProxyRequestAutoInstrumentationTriggerFixtureCoreLatest : AspNetCoreWebApiLambdaFixtureBase
    {
        public LambdaAPIGatewayProxyRequestAutoInstrumentationTriggerFixtureCoreLatest() : base(CoreLatestTFM)
        {
            CommandLineArguments = "--handler APIGatewayProxyFunctionEntryPoint";
        }
    }

    public class LambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTriggerFixtureCoreLatest : AspNetCoreWebApiLambdaFixtureBase
    {
        public LambdaAPIGatewayHttpApiV2ProxyRequestAutoInstrumentationTriggerFixtureCoreLatest() : base(CoreLatestTFM)
        {
            CommandLineArguments = "--handler APIGatewayHttpApiV2ProxyFunctionEntryPoint";
        }
    }
    public class LambdaApplicationLoadBalancerRequestAutoInstrumentationTriggerFixtureCoreLatest : AspNetCoreWebApiLambdaFixtureBase
    {
        public LambdaApplicationLoadBalancerRequestAutoInstrumentationTriggerFixtureCoreLatest() : base(CoreLatestTFM)
        {
            CommandLineArguments = "--handler ApplicationLoadBalancerFunctionEntryPoint";
        }
    }
}
