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
    public class AwsSdkFirehoseController : ControllerBase
    {
        private readonly ILogger<AwsSdkFirehoseController> _logger;

        public AwsSdkFirehoseController(ILogger<AwsSdkFirehoseController> logger)
        {
            _logger = logger;

            _logger.LogInformation("Created AwsSdkFirehoseController");
        }

        [HttpGet("CreateDeliveryStreamAsync")]
        public async Task CreateDeliveryStreamAsync([Required] string streamName, [Required] string bucketName)
        {
            _logger.LogInformation("Starting Firehose CreateDeliveryStreamAsync {streamName} {bucketName}", streamName, bucketName);

            using var awsSdkFirehoseExerciser = new AwsSdkFirehoseExerciser();

            await awsSdkFirehoseExerciser.CreateDeliveryStreamAsync(streamName, bucketName);
            _logger.LogInformation("Finished CreateDeliveryStreamAsync for {streamName} {bucketName}", streamName, bucketName);
        }

        [HttpGet("DeleteDeliveryStreamAsync")]
        public async Task DeleteDeliveryStreamAsync([Required] string streamName)
        {
            _logger.LogInformation("Starting Firehose DeleteDeliveryStreamAsync {streamName}", streamName);

            using var awsSdkFirehoseExerciser = new AwsSdkFirehoseExerciser();

            await awsSdkFirehoseExerciser.DeleteDeliveryStreamAsync(streamName);
            _logger.LogInformation("Finished DeleteDeliveryStreamAsync for {streamName}", streamName);
        }

        [HttpGet("ListDeliveryStreamsAsync")]
        public async Task ListDeliveryStreamsAsync()
        {
            _logger.LogInformation("Starting Firehose ListDeliveryStreamsAsync");

            using var awsSdkFirehoseExerciser = new AwsSdkFirehoseExerciser();

            await awsSdkFirehoseExerciser.ListDeliveryStreamsAsync();
            _logger.LogInformation("Finished ListDeliveryStreamsAsync");
        }

        [HttpGet("PutRecordAsync")]
        public async Task PutRecordAsync([Required] string streamName, [Required] string data)
        {
            _logger.LogInformation("Starting Firehose PutRecordAsync {streamName} {data}", streamName, data);

            using var awsSdkFirehoseExerciser = new AwsSdkFirehoseExerciser();

            await awsSdkFirehoseExerciser.PutRecordAsync(streamName, data);
            _logger.LogInformation("Finished PutRecordAsync");
        }

        [HttpGet("PutRecordBatchAsync")]
        public async Task PutRecordBatchAsync([Required] string streamName, [Required] string data)
        {
            _logger.LogInformation("Starting Firehose PutRecordBatchAsync {streamName} {data}", streamName, data);

            using var awsSdkFirehoseExerciser = new AwsSdkFirehoseExerciser();

            await awsSdkFirehoseExerciser.PutRecordBatchAsync(streamName, data);
            _logger.LogInformation("Finished PutRecordBatchAsync");
        }

    }
}
