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
        private readonly ILogger<HttpTriggerFunctionUsingAspNetCorePipeline> _logger;

        public HttpTriggerFunctionUsingAspNetCorePipeline(ILogger<HttpTriggerFunctionUsingAspNetCorePipeline> logger)
        {
            _logger = logger;
        }

        [Function("HttpTriggerFunctionUsingAspNetCorePipeline")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("HttpTriggerFunctionUsingAspNetCorePipeline processed a request.");

            return new OkObjectResult("Welcome to Azure Functions!");
        }
    }
}
