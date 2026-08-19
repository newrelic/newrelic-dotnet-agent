// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NewRelic.Agent.IntegrationTests.Shared;

namespace AzureFunctionApplication;

/// <summary>
/// A function that logs and then throws, to exercise the isolated-worker HTTP trigger failure path.
/// </summary>
public class HttpTriggerFunctionThatThrows
{
    private readonly ILogger<HttpTriggerFunctionThatThrows> _logger;

    public HttpTriggerFunctionThatThrows(ILogger<HttpTriggerFunctionThatThrows> logger)
    {
        _logger = logger;
    }

    [Function("HttpTriggerFunctionThatThrows")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData reqData)
    {
        _logger.LogInformation(AzureFunctionConfiguration.FuncTestPreExceptionLogMessage);

        await Task.Yield();

        throw new InvalidOperationException("HttpTriggerFunctionThatThrows always throws.");
    }
}
