// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace AwsSdkTestApp.AwsSdkExerciser
{
    public class AwsSdkExerciser : IDisposable
    {
        public AwsSdkExerciser(AwsSdkTestType testType)
        {
            switch (testType)
            {
                case AwsSdkTestType.SQS:
                    _amazonSqsClient = GetSqsClient();
                    break;
                default:
                    throw new ArgumentException("Invalid test type");
            }
        }
        #region SQS

        private readonly AmazonSQSClient _amazonSqsClient;
        private string _sqsQueueUrl = null;

        private AmazonSQSClient GetSqsClient()
        {
            // configure the client to use LocalStack
            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials("dummy", "dummy");
            var config = new AmazonSQSConfig
            {
                ServiceURL = "http://localstack-main:4566",
                AuthenticationRegion = "us-west-2"
            };

            var sqsClient = new AmazonSQSClient(awsCredentials, config);
            return sqsClient;
        }

        private async Task<string> SQS_CreateQueueAsync(string queueName)
        {
            var response = await _amazonSqsClient.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = queueName,
            });

            await Task.Delay(TimeSpan.FromSeconds(1)); // Wait for the queue to be created

            return response.QueueUrl;
        }

        private async Task SQS_DeleteQueueAsync()
        {
            await _amazonSqsClient.DeleteQueueAsync(new DeleteQueueRequest
            {
                QueueUrl = _sqsQueueUrl
            });
        }
        public async Task SQS_Initialize(string queueName)
        {
            if (_sqsQueueUrl != null)
            {
                throw new InvalidOperationException("Queue URL is already set. Call SQS_Teardown first.");
            }

            _sqsQueueUrl = await SQS_CreateQueueAsync(queueName);
        }

        public async Task SQS_Teardown()
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize first.");
            }

            await SQS_DeleteQueueAsync();
            _sqsQueueUrl = null;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SQS_SendMessage(string message)
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize first.");
            }

            await _amazonSqsClient.SendMessageAsync(_sqsQueueUrl, message);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SQS_ReceiveMessage()
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize first.");
            }

            var response = await _amazonSqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
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

        public void Dispose()
        {
            _amazonSqsClient?.Dispose();
        }
    }
}
