/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using OpenTracing.Util;
using NewRelic.OpenTracing.AmazonLambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System.Net;
using System;
using System.Net.Http;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsLambdaChainCallingApplication
{
    public class Function
    {

        public Function()
        {
            GlobalTracer.Register(LambdaTracer.Instance);
        }

        public APIGatewayProxyResponse FunctionWrapper(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return new TracingRequestHandler().LambdaWrapper(FunctionHandler, request, context);
        }

        APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            var calleeUrl = input.QueryStringParameters["calleeUrl"];

            var response = new APIGatewayProxyResponse
            {
                StatusCode = 200
            };

            using (var client = new HttpClient())
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var webResponse = client.GetAsync(calleeUrl).Result;

                    response.Body = "Success";
                }
                catch (Exception e)
                {
                    response.Body = e.Message;
                }
            }

            return response;
        }
    }
}
