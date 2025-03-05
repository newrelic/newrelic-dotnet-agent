// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AzureFunctionInProcApplication
{
    public static class HttpTriggerFunction
    {
        private static bool _firstTime = true;

        [FunctionName("HttpTriggerFunction")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req, ILogger logger)
        {
            logger.LogInformation("HttpTriggerFunction processed a request.");

            if (_firstTime)
            {
                await Task.Delay(250); // to ensure that the first invocation gets sampled
                _firstTime = false;
            }

            return new OkObjectResult("Welcome to Azure in-proc Functions!");
        }
    }
}
