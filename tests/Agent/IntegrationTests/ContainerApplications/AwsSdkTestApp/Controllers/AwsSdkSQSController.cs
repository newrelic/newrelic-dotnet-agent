// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using AwsSdkTestApp.AwsSdkExercisers;
using AwsSdkTestApp.SQSBackgroundService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AwsSdkTestApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AwsSdkSQSController : ControllerBase
    {
        private readonly ILogger<AwsSdkSQSController> _logger;
        private readonly ISQSRequestQueue _requestQueue;
        private readonly ISQSResponseQueue _responseQueue;

        public AwsSdkSQSController(ILogger<AwsSdkSQSController> logger, ISQSRequestQueue requestQueue, ISQSResponseQueue responseQueue)
        {
            _logger = logger;
            _requestQueue = requestQueue;
            _responseQueue = responseQueue;

            _logger.LogInformation("Created AwsSdkController");
        }

        // GET: /AwsSdk/SQS_SendReceivePurge?queueName=MyQueue
        [HttpGet("SQS_SendReceivePurge")]
        public async Task SQS_SendReceivePurgeAsync([Required]string queueName)
        {
            _logger.LogInformation("Starting SQS_SendReceivePurge for {Queue}", queueName);

            using var awsSdkSQSExerciser = new AwsSdkSQSExerciser();
            
            await awsSdkSQSExerciser.SQS_InitializeAsync(queueName);

            await awsSdkSQSExerciser.SQS_SendMessageAsync("Hello World!");
            await awsSdkSQSExerciser.SQS_ReceiveMessageAsync();

            var messages = new[] { "Hello", "World" };
            await awsSdkSQSExerciser.SQS_SendMessageBatchAsync(messages);
            await awsSdkSQSExerciser.SQS_ReceiveMessageAsync(messages.Length);

            await awsSdkSQSExerciser.SQS_PurgeQueueAsync();

            await awsSdkSQSExerciser.SQS_TeardownAsync();

            _logger.LogInformation("Finished SQS_SendReceivePurge for {Queue}", queueName);
        }

        /// <summary>
        /// Creates a queue and returns the queue URL
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        // GET: /AwsSdk/SQS_InitializeQueue?queueName=MyQueue
        [HttpGet("SQS_InitializeQueue")]
        public async Task<string> SQS_InitializeQueueAsync([Required]string queueName)
        {
            _logger.LogInformation("Initializing queue {Queue}", queueName);
            using var awsSdkSQSExerciser = new AwsSdkSQSExerciser();
            var queueUrl = await awsSdkSQSExerciser.SQS_InitializeAsync(queueName);
            _logger.LogInformation("Queue {Queue} initialized with URL {QueueUrl}", queueName, queueUrl);
            return queueUrl;
        }

        // GET: /AwsSdk/SQS_SendMessageToQueue?message=Hello&messageQueueUrl=MyQueue
        [HttpGet("SQS_SendMessageToQueue")]
        public async Task SQS_SendMessageToQueueAsync([Required]string message, [Required]string messageQueueUrl)
        {
            _logger.LogInformation("Sending message {Message} to {Queue}", message, messageQueueUrl);
            using var awsSdkSQSExerciser = new AwsSdkSQSExerciser();
            awsSdkSQSExerciser.SQS_SetQueueUrl(messageQueueUrl);

            await awsSdkSQSExerciser.SQS_SendMessageAsync(message);
            _logger.LogInformation("Message {Message} sent to {Queue}", message, messageQueueUrl);
        }

        // GET: /AwsSdk/SQS_SendMessageBatchToQueue?messageQueueUrl=MyQueue
        [HttpGet("SQS_ReceiveMessageFromQueue")]
        public async Task<IEnumerable<Message>> SQS_ReceiveMessageFromQueueAsync([Required]string messageQueueUrl)
        {
            _logger.LogInformation("Requesting a message from {Queue}", messageQueueUrl);
            await _requestQueue.QueueRequestAsync(messageQueueUrl);
            _logger.LogInformation("Waiting for a response from {Queue}", messageQueueUrl);
            var response = await _responseQueue.DequeueAsync(CancellationToken.None);
            _logger.LogInformation("Received a response: {Response}", response);
            return response;
        }

        // GET: /AwsSdk/SQS_SendMessageBatchToQueue?messageQueueUrl=MyQueue
        [HttpGet("SQS_DeleteQueue")]
        public async Task SQS_DeleteQueueAsync([Required]string messageQueueUrl)
        {
            _logger.LogInformation("Deleting queue {Queue}", messageQueueUrl);
            using var awsSdkSQSExerciser = new AwsSdkSQSExerciser();
            awsSdkSQSExerciser.SQS_SetQueueUrl(messageQueueUrl);

            await awsSdkSQSExerciser.SQS_TeardownAsync();
            _logger.LogInformation("Queue {Queue} deleted", messageQueueUrl);
        }
    }
}
