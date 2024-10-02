// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using AwsSdkTestApp.AwsSdkExerciser;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;

namespace AwsSdkTestApp.SQSBackgroundService
{
    public class SQSReceiverService : BackgroundService
    {
        private readonly ILogger<SQSReceiverService> _logger;
        private readonly ISQSRequestQueue _requestQueue;
        private readonly ISQSResponseQueue _responseQueue;
        private CancellationToken _stoppingToken;

        public SQSReceiverService(ILogger<SQSReceiverService> logger, ISQSRequestQueue requestQueue, ISQSResponseQueue responseQueue)
        {
            _logger = logger;
            _requestQueue = requestQueue;
            _responseQueue = responseQueue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Waiting for a request to receive a message");
                    var queueUrl = await _requestQueue.DequeueAsync(stoppingToken);
                    var messages = await ProcessRequestAsync(queueUrl);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Cancellation requested. Shutting down SQSReceiverService");
                }

                catch (Exception e)
                {
                    _logger.LogError(e, "An error occurred while processing a request");
                    throw;
                }
            }
        }

        [Transaction]
        private async Task<IEnumerable<Message>> ProcessRequestAsync(string queueUrl)
        {
            _logger.LogInformation("Received a request to receive a message from {Queue}", queueUrl);
            using var awsSdkExerciser = new AwsSdkExerciser.AwsSdkExerciser(AwsSdkTestType.SQS);
            awsSdkExerciser.SQS_SetQueueUrl(queueUrl);
            _logger.LogInformation("Receiving a message from {Queue}", queueUrl);
            var messages = await awsSdkExerciser.SQS_ReceiveMessageAsync();
            _logger.LogInformation("Received a message from {Queue}; queuing a response", queueUrl);
            await _responseQueue.QueueResponseAsync(messages);
            _logger.LogInformation("Finished processing request for {Queue}", queueUrl);
            return messages;
        }
    }
}
