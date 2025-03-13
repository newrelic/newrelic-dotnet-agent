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
    public class AwsSdkKinesisController : ControllerBase
    {
        private readonly ILogger<AwsSdkDynamoDBController> _logger;

        public AwsSdkKinesisController(ILogger<AwsSdkDynamoDBController> logger)
        {
            _logger = logger;

            _logger.LogInformation("Created AwsSdkDynamoDBController");
        }

        // GET: /AwsSdkKinesis/CreateStreamAsync?streamName=streamName
        [HttpGet("CreateStreamAsync")]
        public async Task CreateStreamAsync([Required] string streamName)
        {
            _logger.LogInformation("Starting Kinesis CreateStreamAsync {streamName}", streamName);

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.CreateStreamAsync(streamName);
            _logger.LogInformation("Finished CreateStreamAsync for {streamName}", streamName);
        }

        // GET: /AwsSdkKinesis/ListStreamsAsync
        [HttpGet("ListStreamsAsync")]
        public async Task ListStreamsAsync()
        {
            _logger.LogInformation("Starting Kinesis ListStreamsAsync");

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.ListStreamsAsync();
            _logger.LogInformation("Finished ListStreamsAsync");
        }


    }
}
