// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AzureFunctionApplication
{
    /// <summary>
    /// A function that does not use the ASP.NET Core pipeline.
    /// </summary>
    public class HttpTriggerFunctionUsingSimpleInvocation
    {
        private static bool _firstTime = true;
        private readonly ILogger<HttpTriggerFunctionUsingSimpleInvocation> _logger;

        public HttpTriggerFunctionUsingSimpleInvocation(ILogger<HttpTriggerFunctionUsingSimpleInvocation> logger)
        {
            _logger = logger;
        }

        [Function("HttpTriggerFunctionUsingSimpleInvocation")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData reqData)
        {
            _logger.LogInformation("HttpTriggerFunctionUsingSimpleInvocation processed a request.");

            if (_firstTime)
            {
                await Task.Delay(250); // to ensure that the first invocation gets sampled
                _firstTime = false;
            }

            var response = reqData.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }
    }
}
