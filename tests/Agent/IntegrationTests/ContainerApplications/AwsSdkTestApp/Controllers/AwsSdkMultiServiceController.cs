// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using AwsSdkTestApp.AwsSdkExercisers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AwsSdkTestApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AwsSdkMultiServiceController : ControllerBase
    {
        private readonly ILogger<AwsSdkMultiServiceController> _logger;

        public AwsSdkMultiServiceController(ILogger<AwsSdkMultiServiceController> logger)
        {
            _logger = logger;
            _logger.LogInformation("Created AwsSdkMultiServiceController");
        }

        [HttpGet("CallMultipleServicesAsync")]
        public async Task CallMultipleServicesAsync([FromQuery, Required]string queueName, [FromQuery, Required]string tableName, [FromQuery, Required]string bookName)
        {
            _logger.LogInformation("Starting CallMultipleServicesAsync");

            using var sqsExerciser = new AwsSdkSQSExerciser();
            using var dynamoDbExerciser = new AwsSdkDynamoDBExerciser();

            await sqsExerciser.SQS_InitializeAsync(queueName);

            // send an SQS message
            await sqsExerciser.SQS_SendMessageAsync(bookName);

            await Task.Delay(TimeSpan.FromSeconds(2)); // may not really be necessary

            // receive an SQS message
            var messages = await sqsExerciser.SQS_ReceiveMessageAsync();

            var movieName = messages.First().Body;

            // create a DynamoDB table
            await dynamoDbExerciser.CreateTableAsync(tableName);
            // put an item in a DynamoDB table
            await dynamoDbExerciser.PutItemAsync(tableName, movieName, "2021");

            // delete the table
            await dynamoDbExerciser.DeleteTableAsync(tableName);

            await sqsExerciser.SQS_TeardownAsync();

            _logger.LogInformation("Finished CallMultipleServicesAsync");
        }
    }
}
