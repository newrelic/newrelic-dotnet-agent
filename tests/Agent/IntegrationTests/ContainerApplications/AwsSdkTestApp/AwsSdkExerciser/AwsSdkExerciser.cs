// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Linq;
using System.Collections.Generic;

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
                ServiceURL = "http://localstack-containertest:4566",
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
        public async Task<string> SQS_InitializeAsync(string queueName)
        {
            if (_sqsQueueUrl != null)
            {
                throw new InvalidOperationException("Queue URL is already set. Call SQS_Teardown first.");
            }

            _sqsQueueUrl = await SQS_CreateQueueAsync(queueName);

            return _sqsQueueUrl;
        }

        public async Task SQS_TeardownAsync()
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize or SQS_SetQueueUrl first.");
            }

            await SQS_DeleteQueueAsync();
            _sqsQueueUrl = null;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SQS_SendMessageAsync(string message)
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize or SQS_SetQueueUrl first.");
            }

            await _amazonSqsClient.SendMessageAsync(_sqsQueueUrl, message);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<IEnumerable<Message>> SQS_ReceiveMessageAsync(int maxMessagesToReceive = 1)
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize or SQS_SetQueueUrl first.");
            }

            var response = await _amazonSqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = _sqsQueueUrl,
                MaxNumberOfMessages = maxMessagesToReceive,
                MessageAttributeNames = ["All"]
            });

            foreach (var message in response.Messages)
            {
                Console.WriteLine($"Message: {message.Body}");
                foreach (var attr in message.MessageAttributes)
                {
                    Console.WriteLine($"MessageAttributes: {attr.Key} = {{ DataType = {attr.Value.DataType}, StringValue = {attr.Value.StringValue}}}");
                }

                // delete message
                await _amazonSqsClient.DeleteMessageAsync(new DeleteMessageRequest
                {
                    QueueUrl = _sqsQueueUrl,
                    ReceiptHandle = message.ReceiptHandle
                });
            }

            return response.Messages;
        }

        // send message batch
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SQS_SendMessageBatchAsync(string[] messages)
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize or SQS_SetQueueUrl first.");
            }

            var request = new SendMessageBatchRequest
            {
                QueueUrl = _sqsQueueUrl,

                Entries = messages.Select(m => new SendMessageBatchRequestEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    MessageBody = m
                }).ToList()
            };

            await _amazonSqsClient.SendMessageBatchAsync(request);
        }

        // purge the queue
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SQS_PurgeQueueAsync()
        {
            if (_sqsQueueUrl == null)
            {
                throw new InvalidOperationException("Queue URL is not set. Call SQS_Initialize or SQS_SetQueueUrl first.");
            }

            await _amazonSqsClient.PurgeQueueAsync(new PurgeQueueRequest
            {
                QueueUrl = _sqsQueueUrl
            });
        }

        public void SQS_SetQueueUrl(string messageQueueUrl)
        {
            _sqsQueueUrl = messageQueueUrl;
        }

        #endregion

        public void Dispose()
        {
            _amazonSqsClient?.Dispose();
        }
    }
}
