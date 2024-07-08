// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using AwsSdkTestApp.AwsSdkExerciser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AwsSdkTestApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AwsSdkController(ILogger<AwsSdkController> logger) : ControllerBase
    {
        private readonly ILogger<AwsSdkController> _logger = logger;

        // GET: /AwsSdk/SQS_SendReceivePurge?queueName=MyQueue
        [HttpGet("SQS_SendReceivePurge")]
        public async Task SQS_SendReceivePurge([Required]string queueName)
        {
            using var awsSdkExerciser = new AwsSdkExerciser.AwsSdkExerciser(AwsSdkTestType.SQS);
            
            await awsSdkExerciser.SQS_Initialize(queueName);

            await awsSdkExerciser.SQS_SendMessage("Hello World!");
            await awsSdkExerciser.SQS_ReceiveMessage();

            var messages = new[] { "Hello", "World" };
            await awsSdkExerciser.SQS_SendMessageBatch(messages);
            await awsSdkExerciser.SQS_ReceiveMessage(messages.Length);

            await awsSdkExerciser.SQS_PurgeQueue();

            await awsSdkExerciser.SQS_Teardown();
        }

        /// <summary>
        /// Creates a queue and returns the queue URL
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        // GET: /AwsSdk/SQS_InitializeQueue?queueName=MyQueue
        [HttpGet("SQS_InitializeQueue")]
        public async Task<string> SQS_InitializeQueue([Required]string queueName)
        {
            using var awsSdkExerciser = new AwsSdkExerciser.AwsSdkExerciser(AwsSdkTestType.SQS);
            return await awsSdkExerciser.SQS_Initialize(queueName);
        }

        // GET: /AwsSdk/SQS_SendMessageToQueue?message=Hello&messageQueueUrl=MyQueue
        [HttpGet("SQS_SendMessageToQueue")]
        public async Task SQS_SendMessageToQueue([Required]string message, [Required]string messageQueueUrl)
        {
            using var awsSdkExerciser = new AwsSdkExerciser.AwsSdkExerciser(AwsSdkTestType.SQS);
            awsSdkExerciser.SQS_SetQueueUrl(messageQueueUrl);

            await awsSdkExerciser.SQS_SendMessage(message);
        }

        // GET: /AwsSdk/SQS_SendMessageBatchToQueue?messageQueueUrl=MyQueue
        [HttpGet("SQS_ReceiveMessageFromQueue")]
        public async Task<IEnumerable<Message>> SQS_ReceiveMessageFromQueue([Required]string messageQueueUrl)
        {
            using var awsSdkExerciser = new AwsSdkExerciser.AwsSdkExerciser(AwsSdkTestType.SQS);
            awsSdkExerciser.SQS_SetQueueUrl(messageQueueUrl);

            var messages = await awsSdkExerciser.SQS_ReceiveMessage();

            return messages;
        }

        // GET: /AwsSdk/SQS_SendMessageBatchToQueue?messageQueueUrl=MyQueue
        [HttpGet("SQS_DeleteQueue")]
        public async Task SQS_DeleteQueue([Required]string messageQueueUrl)
        {
            using var awsSdkExerciser = new AwsSdkExerciser.AwsSdkExerciser(AwsSdkTestType.SQS);
            awsSdkExerciser.SQS_SetQueueUrl(messageQueueUrl);

            await awsSdkExerciser.SQS_Teardown();
        }
    }
}
