// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using W3CTestApp.Models;

namespace W3CTestApp.Controllers;

[ApiController]
public class W3CController : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// This is a test endpoint to allow the service to take in test json that calls back into itself.
    /// </summary>
    [HttpPost("drop")]
    public async Task<IActionResult> DropThisData()
    {
        var value = await ReadBodyAsync();
        if (string.IsNullOrEmpty(value))
        {
            return BadRequest("POST body is null.");
        }

        return Ok();
    }

    /// <summary>
    /// This is the test endpoint. The W3C test harness (python) will call this.
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test()
    {
        var value = await ReadBodyAsync();
        if (string.IsNullOrEmpty(value))
        {
            return BadRequest("POST body is null.");
        }

        var models = JsonSerializer.Deserialize<List<W3CTestModel>>(value, _jsonOptions);
        ProcessModels(models);
        return Ok();
    }

    private async Task<string> ReadBodyAsync()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private void ProcessModels(List<W3CTestModel> models)
    {
        // Outbound calls are made synchronously and in order so the agent injects the
        // W3C trace context headers (traceparent/tracestate) that the harness validates.
        using var client = new HttpClient();
        foreach (var model in models)
        {
            var request = BuildHttpRequest(model);
            _ = client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .Result;
        }
    }

    private HttpRequestMessage BuildHttpRequest(W3CTestModel model)
    {
        var argumentsJson = JsonSerializer.Serialize(model.Arguments, _jsonOptions);
        return new HttpRequestMessage(HttpMethod.Post, model.Url)
        {
            Content = new StringContent(argumentsJson, Encoding.UTF8, "application/json")
        };
    }
}
