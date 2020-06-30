/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using OpenTracing.Util;
using NewRelic.OpenTracing.AmazonLambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using System.Collections.Generic;
using System;
using System.Net.Http;
using Amazon.SimpleNotificationService;
using Amazon.SQS.Model;
using Amazon.SimpleNotificationService.Model;
using Amazon;
using Amazon.SQS;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsLambdaAwsSdkTestFunction
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
            var accessKeyId = input.QueryStringParameters["AwsAccessKeyId"];
            var secretAccessKey = input.QueryStringParameters["AwsSecretAccessKey"];
            var accountNumber = input.QueryStringParameters["AwsAccountNumber"];

            if (string.IsNullOrEmpty(accessKeyId) &&
                string.IsNullOrEmpty(secretAccessKey) &&
                string.IsNullOrEmpty(accountNumber))
            {
                throw new Exception("AwsAccessKeyId, AwsSecretAccessKey, and AwsAccountNumber query string parameters  must not be empty");
            }

            TestDynamoDB(accessKeyId, secretAccessKey);
            TestSQS(accessKeyId, secretAccessKey, accountNumber);
            SendToSNS(accessKeyId, secretAccessKey, accountNumber);
            SendToSNSPhoneNumber(accessKeyId, secretAccessKey);
            TestHttpSpan();
            TestHttpError();

            var response = new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = "Function Executed"
            };

            return response;
        }

        public void TestDynamoDB(string accessKeyId, string secretAccessKey)
        {
            var dbClient = new AmazonDynamoDBClient(accessKeyId, secretAccessKey, RegionEndpoint.USWest2);
            GetItemOperationConfig config = new GetItemOperationConfig
            {
                AttributesToGet = new List<string> { "Name" },
                ConsistentRead = true
            };
            var table = Table.LoadTable(dbClient, "DotNetTest");
            var item = table.GetItemAsync("John", config).Result;
        }

        public void TestSQS(string accessKeyId, string secretAccessKey, string accountNumber)
        {
            var client = new AmazonSQSClient(accessKeyId, secretAccessKey, RegionEndpoint.USWest2);

            var request = new SendMessageRequest
            {
                DelaySeconds = (int)TimeSpan.FromSeconds(5).TotalSeconds,
                MessageBody = "John Doe customer information.",
                QueueUrl = $@"https://sqs.us-west-2.amazonaws.com/{accountNumber}/DotnetTestSQS"
            };
            var response = client.SendMessageAsync(request).Result;

            var rrequest = new ReceiveMessageRequest
            {
                QueueUrl = $@"https://sqs.us-west-2.amazonaws.com/{accountNumber}/DotnetTestSQS"
            };
            var rresponse = client.ReceiveMessageAsync(rrequest).Result;
        }

        public void SendToSNS(string accessKeyId, string secretAccessKey, string accountNumber)
        {
            var client = new AmazonSimpleNotificationServiceClient(accessKeyId, secretAccessKey, RegionEndpoint.USWest2);
            var request = new PublishRequest
            {
                TopicArn = $"arn:aws:sns:us-west-2:{accountNumber}:DotNetTestSNSTopic",
                Message = "Test Message"
            };
            var response = client.PublishAsync(request).Result;
        }

        public void SendToSNSPhoneNumber(string accessKeyId, string secretAccessKey)
        {
            var client = new AmazonSimpleNotificationServiceClient(accessKeyId, secretAccessKey, RegionEndpoint.USWest2);
            var request = new PublishRequest
            {
                PhoneNumber = "+15035550100", // example number for testing per AWS https://docs.aws.amazon.com/sns/latest/dg/sms_publish-to-phone.html
                Message = "Test Message"
            };
            var response = client.PublishAsync(request).Result;
        }

        public void TestHttpSpan()
        {
            string url = "http://www.newrelic.com/";
            // Make an HttpClient call
            HttpClient client = new HttpClient();
            // Just call it synchronously for simplicity
            _ = client.GetAsync(url).GetAwaiter().GetResult();
        }

        public void TestHttpError()
        {
            try
            {
                string url = "http://www.b-a-d.url/";
                // Make an HttpClient call
                HttpClient client = new HttpClient();
                // Just call it synchronously for simplicity
                HttpResponseMessage ret = client.GetAsync(url).GetAwaiter().GetResult();
            }
            catch (HttpRequestException) { }
        }
    }
}
