// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
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
            await awsSdkExerciser.SQS_SendMessageBatch(new[] { "Hello", "World" });
            await awsSdkExerciser.SQS_PurgeQueue();

            await awsSdkExerciser.SQS_Teardown();
        }
    }
}
