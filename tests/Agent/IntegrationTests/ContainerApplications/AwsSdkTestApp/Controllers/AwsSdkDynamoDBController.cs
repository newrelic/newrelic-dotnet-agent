// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using AwsSdkTestApp.AwsSdkExercisers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AwsSdkTestApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AwsSdkDynamoDBController : ControllerBase
    {
        private readonly ILogger<AwsSdkDynamoDBController> _logger;

        public AwsSdkDynamoDBController(ILogger<AwsSdkDynamoDBController> logger)
        {
            _logger = logger;

            _logger.LogInformation("Created AwsSdkDynamoDBController");
        }

        // GET: /AwsSdkDynamoDB/CreateTable?tableName=tableName
        [HttpGet("CreateTableAsync")]
        public async Task CreateTableAsync([Required] string tableName)
        {
            _logger.LogInformation("Starting DynamoDB CreateTableAsync {tableName}", tableName);

            using var awsSdkDynamoDBExerciser = new AwsSdkDynamoDBExerciser();

            await awsSdkDynamoDBExerciser.CreateTableAsync(tableName);
            _logger.LogInformation("Finished CreateTableAsync for {tableName}", tableName);
        }

        // GET: /AwsSdkDynamoDB/PutItemAsync?tableName=tableName&title=title&year=year
        [HttpGet("PutItemAsync")]
        public async Task PutItemAsync([Required] string tableName, string title, int year)
        {
            _logger.LogInformation("Starting DynamoDB PutItemAsync {tableName} {title} {year}", tableName, title, year);

            using var awsSdkDynamoDBExerciser = new AwsSdkDynamoDBExerciser();

            await awsSdkDynamoDBExerciser.PutItemAsync(tableName, title, year);
            _logger.LogInformation("Finished PutItemAsync for {title} {year}", title, year);
        }

    }
}
