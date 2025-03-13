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
        private readonly ILogger<AwsSdkKinesisController> _logger;

        public AwsSdkKinesisController(ILogger<AwsSdkKinesisController> logger)
        {
            _logger = logger;

            _logger.LogInformation("Created AwsSdkKinesisController");
        }

        [HttpGet("CreateStreamAsync")]
        public async Task CreateStreamAsync([Required] string streamName)
        {
            _logger.LogInformation("Starting Kinesis CreateStreamAsync {streamName}", streamName);

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.CreateStreamAsync(streamName);
            _logger.LogInformation("Finished CreateStreamAsync for {streamName}", streamName);
        }

        [HttpGet("DeleteStreamAsync")]
        public async Task DeleteStreamAsync([Required] string streamName)
        {
            _logger.LogInformation("Starting Kinesis DeleteStreamAsync {streamName}", streamName);

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.DeleteStreamAsync(streamName);
            _logger.LogInformation("Finished DeleteStreamAsync for {streamName}", streamName);
        }

        [HttpGet("ListStreamsAsync")]
        public async Task ListStreamsAsync()
        {
            _logger.LogInformation("Starting Kinesis ListStreamsAsync");

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.ListStreamsAsync();
            _logger.LogInformation("Finished ListStreamsAsync");
        }

        [HttpGet("RegisterStreamConsumerAsync")]
        public async Task RegisterStreamConsumerAsync([Required] string streamName, [Required] string consumerName)
        {
            _logger.LogInformation("Starting Kinesis RegisterStreamConsumerAsync {streamName} {consumerName}", streamName, consumerName);

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.RegisterStreamConsumerAsync(streamName, consumerName);
            _logger.LogInformation("Finished RegisterStreamConsumerAsync");
        }

        [HttpGet("DeregisterStreamConsumerAsync")]
        public async Task DeregisterStreamConsumerAsync([Required] string streamName, [Required] string consumerName)
        {
            _logger.LogInformation("Starting Kinesis DeregisterStreamConsumerAsync {streamName} {consumerName}", streamName, consumerName);

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.DeregisterStreamConsumerAsync(streamName, consumerName);
            _logger.LogInformation("Finished DeregisterStreamConsumerAsync");
        }

        [HttpGet("ListStreamConsumersAsync")]
        public async Task ListStreamConsumersAsync([Required] string streamName)
        {
            _logger.LogInformation("Starting Kinesis ListStreamConsumersAsync {streamName}", streamName);

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.ListStreamConsumersAsync(streamName);
            _logger.LogInformation("Finished ListStreamConsumersAsync");
        }

        [HttpGet("PutRecordAsync")]
        public async Task PutRecordAsync([Required] string streamName, [Required] string data)
        {
            _logger.LogInformation("Starting Kinesis PutRecordAsync {streamName} {data}", streamName, data);

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.PutRecordAsync(streamName, data);
            _logger.LogInformation("Finished PutRecordAsync");
        }

        [HttpGet("PutRecordsAsync")]
        public async Task PutRecordsAsync([Required] string streamName, [Required] string data)
        {
            _logger.LogInformation("Starting Kinesis PutRecordsAsync {streamName} {data}", streamName, data);

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.PutRecordsAsync(streamName, data);
            _logger.LogInformation("Finished PutRecordsAsync");
        }

        [HttpGet("GetRecordsAsync")]
        public async Task GetRecordsAsync([Required] string streamName)
        {
            _logger.LogInformation("Starting Kinesis GetRecordsAsync {streamName}", streamName);

            using var awsSdkKinesisExerciser = new AwsSdkKinesisExerciser();

            await awsSdkKinesisExerciser.GetRecordsAsync(streamName);
            _logger.LogInformation("Finished GetRecordsAsync");
        }


    }
}
