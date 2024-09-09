// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureFunctionApplication
{
    public class QueueTriggerFunction
    {

        private readonly ILogger<QueueTriggerFunction> _logger;

        public QueueTriggerFunction(ILogger<QueueTriggerFunction> logger)
        {
            _logger = logger;
        }

        [Function("QueueTriggerFunction")]
        public void Run([QueueTrigger("my-queue")] string message)
        {
            _logger.LogInformation($"C# Queue trigger function processed: {message}");
        }
    }
}
