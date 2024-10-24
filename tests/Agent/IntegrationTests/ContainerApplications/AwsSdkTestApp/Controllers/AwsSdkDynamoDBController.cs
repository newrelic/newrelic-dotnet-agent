// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
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

        // GET: /AwsSdkDynamoDB/DeleteTable?tableName=tableName
        [HttpGet("DeleteTableAsync")]
        public async Task DeleteTableAsync([Required] string tableName)
        {
            _logger.LogInformation("Starting DynamoDB DeleteTableAsync {tableName}", tableName);

            using var awsSdkDynamoDBExerciser = new AwsSdkDynamoDBExerciser();

            await awsSdkDynamoDBExerciser.DeleteTableAsync(tableName);
            _logger.LogInformation("Finished DeleteTableAsync for {tableName}", tableName);
        }

        // GET: /AwsSdkDynamoDB/PutItemAsync?tableName=tableName&title=title&year=year
        [HttpGet("PutItemAsync")]
        public async Task PutItemAsync([Required] string tableName, string title, string year)
        {
            _logger.LogInformation("Starting DynamoDB PutItemAsync {tableName} {title} {year}", tableName, title, year);

            using var awsSdkDynamoDBExerciser = new AwsSdkDynamoDBExerciser();

            await awsSdkDynamoDBExerciser.PutItemAsync(tableName, title, year);
            _logger.LogInformation("Finished PutItemAsync for {title} {year}", title, year);
        }

        // GET: /AwsSdkDynamoDB/GetItemAsync?tableName=tableName&title=title&year=year
        [HttpGet("GetItemAsync")]
        public async Task GetItemAsync([Required] string tableName, string title, string year)
        {
            _logger.LogInformation("Starting DynamoDB GetItemAsync {tableName} {title} {year}", tableName, title, year);

            using var awsSdkDynamoDBExerciser = new AwsSdkDynamoDBExerciser();

            await awsSdkDynamoDBExerciser.GetItemAsync(tableName, title, year);
            _logger.LogInformation("Finished GetItemAsync for {title} {year}", title, year);
        }

        // GET: /AwsSdkDynamoDB/UpdateItemAsync?tableName=tableName&title=title&year=year
        [HttpGet("UpdateItemAsync")]
        public async Task UpdateItemAsync([Required] string tableName, string title, string year)
        {
            _logger.LogInformation("Starting DynamoDB UpdateItemAsync {tableName} {title} {year}", tableName, title, year);

            using var awsSdkDynamoDBExerciser = new AwsSdkDynamoDBExerciser();

            await awsSdkDynamoDBExerciser.UpdateItemAsync(tableName, title, year);
            _logger.LogInformation("Finished UpdateItemAsync for {title} {year}", title, year);
        }

        // GET: /AwsSdkDynamoDB/DeleteItemAsync?tableName=tableName&title=title&year=year
        [HttpGet("DeleteItemAsync")]
        public async Task DeleteItemAsync([Required] string tableName, string title, string year)
        {
            _logger.LogInformation("Starting DynamoDB DeleteItemAsync {tableName} {title} {year}", tableName, title, year);

            using var awsSdkDynamoDBExerciser = new AwsSdkDynamoDBExerciser();

            await awsSdkDynamoDBExerciser.DeleteItemAsync(tableName, title, year);
            _logger.LogInformation("Finished DeleteItemAsync for {title} {year}", title, year);
        }

        // GET: /AwsSdkDynamoDB/QueryAsync?tableName=tableName&title=title&year=year
        [HttpGet("QueryAsync")]
        public async Task QueryAsync([Required] string tableName, string title, string year)
        {
            _logger.LogInformation("Starting DynamoDB QueryAsync {tableName} {title} {year}", tableName, title, year);

            using var awsSdkDynamoDBExerciser = new AwsSdkDynamoDBExerciser();

            await awsSdkDynamoDBExerciser.QueryAsync(tableName, title, year);
            _logger.LogInformation("Finished QueryAsync for {title} {year}", title, year);
        }

        // GET: /AwsSdkDynamoDB/ScanAsync?tableName=tableName
        [HttpGet("ScanAsync")]
        public async Task ScanAsync([Required] string tableName)
        {
            _logger.LogInformation("Starting DynamoDB ScanAsync {tableName}", tableName);

            using var awsSdkDynamoDBExerciser = new AwsSdkDynamoDBExerciser();

            await awsSdkDynamoDBExerciser.ScanAsync(tableName);
            _logger.LogInformation("Finished ScanAsync for {tableName}", tableName);
        }

    }
}
