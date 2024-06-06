// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        private static string GetHandlerString(bool isAsync)
        {
            return "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::ApiGatewayHttpApiV2ProxyRequestHandler"+ (isAsync ? "Async" : "");
        }

        protected LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureBase(string targetFramework, bool isAsync) :
            base(targetFramework,
                null,
                GetHandlerString(isAsync),
                "ApiGatewayHttpApiV2ProxyRequestHandler" + (isAsync ? "Async" : ""),
                null)
        {
        }

        public void EnqueueAPIGatewayHttpApiV2ProxyRequest()
        {
            var apiGatewayProxyRequestJson = $$"""
                                               {
                                                 "version": "2.0",
                                                 "routeKey": "$default",
                                                 "rawPath": "/path/to/resource",
                                                 "rawQueryString": "parameter1=value1&parameter1=value2&parameter2=value",
                                                 "cookies": [
                                                   "cookie1",
                                                   "cookie2"
                                                 ],
                                                 "headers": {
                                                   "Header1": "value1",
                                                   "Header2": "value1,value2",
                                                   "X-Forwarded-For": "127.0.0.1, 127.0.0.2",
                                                   "X-Forwarded-Port": "443",
                                                   "X-Forwarded-Proto": "https"
                                                 },
                                                 "queryStringParameters": {
                                                   "parameter1": "value1,value2",
                                                   "parameter2": "value"
                                                 },
                                                 "requestContext": {
                                                   "accountId": "123456789012",
                                                   "apiId": "api-id",
                                                   "authentication": {
                                                     "clientCert": {
                                                       "clientCertPem": "CERT_CONTENT",
                                                       "subjectDN": "www.example.com",
                                                       "issuerDN": "Example issuer",
                                                       "serialNumber": "a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1",
                                                       "validity": {
                                                         "notBefore": "May 28 12:30:02 2019 GMT",
                                                         "notAfter": "Aug  5 09:36:04 2021 GMT"
                                                       }
                                                     }
                                                   },
                                                   "authorizer": {
                                                     "jwt": {
                                                       "claims": {
                                                         "claim1": "value1",
                                                         "claim2": "value2"
                                                       },
                                                       "scopes": [
                                                         "scope1",
                                                         "scope2"
                                                       ]
                                                     }
                                                   },
                                                   "domainName": "id.execute-api.us-east-1.amazonaws.com",
                                                   "domainPrefix": "id",
                                                   "http": {
                                                     "method": "POST",
                                                     "path": "/path/to/resource",
                                                     "protocol": "HTTP/1.1",
                                                     "sourceIp": "192.168.0.1/32",
                                                     "userAgent": "agent"
                                                   },
                                                   "requestId": "id",
                                                   "routeKey": "$default",
                                                   "stage": "$default",
                                                   "time": "12/Mar/2020:19:03:58 +0000",
                                                   "timeEpoch": 1583348638390
                                                 },
                                                 "body": "eyJ0ZXN0IjoiYm9keSJ9",
                                                 "pathParameters": {
                                                   "parameter1": "value1"
                                                 },
                                                 "isBase64Encoded": true,
                                                 "stageVariables": {
                                                   "stageVariable1": "value1",
                                                   "stageVariable2": "value2"
                                                 }
                                               }
                                               """;
            EnqueueLambdaEvent(apiGatewayProxyRequestJson);
        }

        public void EnqueueAPIGatewayHttpApiV2ProxyRequestWithDTHeaders(string traceId, string spanId)
        {
            var apiGatewayProxyRequestJson = $$"""
                                               {
                                                 "version": "2.0",
                                                 "routeKey": "$default",
                                                 "rawPath": "/path/to/resource",
                                                 "rawQueryString": "parameter1=value1&parameter1=value2&parameter2=value",
                                                 "cookies": [
                                                   "cookie1",
                                                   "cookie2"
                                                 ],
                                                 "headers": {
                                                   "Header1": "value1",
                                                   "Header2": "value1,value2",
                                                   "X-Forwarded-For": "127.0.0.1, 127.0.0.2",
                                                   "X-Forwarded-Port": "443",
                                                   "X-Forwarded-Proto": "https",
                                                   "traceparent": "{{GetTestTraceParentHeaderValue(traceId, spanId)}}",
                                                   "tracestate": "{{GetTestTraceStateHeaderValue(spanId)}}"
                                                 },
                                                 "queryStringParameters": {
                                                   "parameter1": "value1,value2",
                                                   "parameter2": "value"
                                                 },
                                                 "requestContext": {
                                                   "accountId": "123456789012",
                                                   "apiId": "api-id",
                                                   "authentication": {
                                                     "clientCert": {
                                                       "clientCertPem": "CERT_CONTENT",
                                                       "subjectDN": "www.example.com",
                                                       "issuerDN": "Example issuer",
                                                       "serialNumber": "a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1:a1",
                                                       "validity": {
                                                         "notBefore": "May 28 12:30:02 2019 GMT",
                                                         "notAfter": "Aug  5 09:36:04 2021 GMT"
                                                       }
                                                     }
                                                   },
                                                   "authorizer": {
                                                     "jwt": {
                                                       "claims": {
                                                         "claim1": "value1",
                                                         "claim2": "value2"
                                                       },
                                                       "scopes": [
                                                         "scope1",
                                                         "scope2"
                                                       ]
                                                     }
                                                   },
                                                   "domainName": "id.execute-api.us-east-1.amazonaws.com",
                                                   "domainPrefix": "id",
                                                   "http": {
                                                     "method": "POST",
                                                     "path": "/path/to/resource",
                                                     "protocol": "HTTP/1.1",
                                                     "sourceIp": "192.168.0.1/32",
                                                     "userAgent": "agent"
                                                   },
                                                   "requestId": "id",
                                                   "routeKey": "$default",
                                                   "stage": "$default",
                                                   "time": "12/Mar/2020:19:03:58 +0000",
                                                   "timeEpoch": 1583348638390
                                                 },
                                                 "body": "eyJ0ZXN0IjoiYm9keSJ9",
                                                 "pathParameters": {
                                                   "parameter1": "value1"
                                                 },
                                                 "isBase64Encoded": true,
                                                 "stageVariables": {
                                                   "stageVariable1": "value1",
                                                   "stageVariable2": "value2"
                                                 }
                                               }
                                               """;
            EnqueueLambdaEvent(apiGatewayProxyRequestJson);
        }

        /// <summary>
        /// A minimal payload to validate the fix for https://github.com/newrelic/newrelic-dotnet-agent/issues/2528
        /// </summary>
        public void EnqueueMinimalAPIGatewayHttpApiV2ProxyRequest()
        {
            var apiGatewayProxyRequestJson = $$"""
                                               {
                                                 "version": "2.0",
                                                 "routeKey": "$default",
                                                 "rawPath": "/path/to/resource",
                                                 "requestContext": {
                                                   "http": {
                                                     "method": "POST",
                                                     "path": "/path/to/resource"
                                                   }
                                                 },
                                                 "body": "{\"test\":\"body\"}"
                                               }
                                               """;
            EnqueueLambdaEvent(apiGatewayProxyRequestJson);
        }
    }

    public class LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureNet6 : LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureBase
    {
        public LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureNet6() : base("net6.0", false) { }
    }

    public class AsyncLambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureNet6 : LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureBase
    {
        public AsyncLambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureNet6() : base("net6.0", true) { }
    }

    public class LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureNet8 : LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureBase
    {
        public LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureNet8() : base("net8.0", false) { }
    }

    public class AsyncLambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureNet8 : LambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureBase
    {
        public AsyncLambdaAPIGatewayHttpApiV2ProxyRequestTriggerFixtureNet8() : base("net8.0", true) { }
    }
}
