// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.AWS
{
    [Library]
    public class AwsSdkExerciser
    {
        #region SQS

        private static readonly AmazonSQSClient AmazonSqsClient = GetSqsClient();
        private static string _sqsQueueUrl = null;

        private static AmazonSQSClient GetSqsClient()
        {
            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(AwsSQSConfiguration.AwsAccessKeyId, AwsSQSConfiguration.AwsSecretAccessKey);

            var config = new AmazonSQSConfig();
            if (string.IsNullOrEmpty(AwsSQSConfiguration.ServiceUrl)) // serviceUrl is null when going directly to AWS, non-null when using LocalStack
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(AwsSQSConfiguration.AwsRegion);
            }
            else
            {
                config.ServiceURL = AwsSQSConfiguration.ServiceUrl;
                config.AuthenticationRegion = AwsSQSConfiguration.AwsRegion; // **NOT** config.RegionEndpoint here
            }

            var sqsClient = new AmazonSQSClient(awsCredentials, config);
            return sqsClient;
        }

        private static async Task<string> SQS_CreateQueueAsync(string queueName)
        {
            var response = await AmazonSqsClient.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = queueName,
            });

            await Task.Delay(TimeSpan.FromSeconds(1)); // Wait for the queue to be created

            return response.QueueUrl;
        }

        private static async Task SQS_DeleteQueueAsync()
        {
            await AmazonSqsClient.DeleteQueueAsync(new DeleteQueueRequest
            {
                QueueUrl = _sqsQueueUrl
            });
        }

        [LibraryMethod]
        public async Task SQS_Initialize(string queueName)
        {
            if (_sqsQueueUrl != null)
            {
                throw new InvalidOperationException("Queue URL is already set. Call SQS_Teardown first.");
            }

            _sqsQueueUrl = await SQS_CreateQueueAsync(queueName);
        }

        [LibraryMethod]
        public async Task SQS_Teardown()
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize first.");
            }

            await SQS_DeleteQueueAsync();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SQS_SendMessage(string message)
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize first.");
            }

            await AmazonSqsClient.SendMessageAsync(_sqsQueueUrl, message);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SQS_ReceiveMessage()
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize first.");
            }

            var response = await AmazonSqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = _sqsQueueUrl,
                MaxNumberOfMessages = 1
            });

            foreach (var message in response.Messages)
            {
                Console.WriteLine($"Message: {message.Body}");
            }
        }
        #endregion

    }
}
