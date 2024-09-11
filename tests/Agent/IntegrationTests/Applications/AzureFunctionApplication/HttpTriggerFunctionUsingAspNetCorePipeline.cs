// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureFunctionApplication
{
    public class HttpTriggerFunctionUsingAspNetCorePipeline
    {
        private static bool _firstTime = true;
        private readonly ILogger<HttpTriggerFunctionUsingAspNetCorePipeline> _logger;

        public HttpTriggerFunctionUsingAspNetCorePipeline(ILogger<HttpTriggerFunctionUsingAspNetCorePipeline> logger)
        {
            _logger = logger;
        }

        [Function("HttpTriggerFunctionUsingAspNetCorePipeline")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("HttpTriggerFunctionUsingAspNetCorePipeline processed a request.");

            if (_firstTime)
            {
                await Task.Delay(500); // to ensure that the first invocation gets sampled
                _firstTime = false;
            }


            return new OkObjectResult("Welcome to Azure Functions!");
        }
    }
}
