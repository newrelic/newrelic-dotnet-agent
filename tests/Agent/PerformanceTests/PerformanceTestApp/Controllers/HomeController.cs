// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;
using NewRelic.Api.Agent;

namespace PerformanceTestApp.Controllers;

[ApiController]
[Route("[controller]")]
public class HomeController : ControllerBase
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    /// <summary>Simple endpoint that returns quickly - baseline transaction overhead.</summary>
    [HttpGet("simple")]
    public IActionResult Simple()
    {
        var timestamp = DateTime.UtcNow;
        _logger.LogInformation("Simple request at {Timestamp}", timestamp);
        return Ok(new { message = "ok", timestamp });
    }

    /// <summary>Nested async calls that simulate a multi-layer service.</summary>
    [HttpGet("nested")]
    public async Task<IActionResult> Nested()
    {
        var step1 = await DoStep("step1", 5);
        var step2 = await DoStep("step2", 5);
        var step3 = await DoStep("step3", 5);

        _logger.LogInformation("Nested completed {Step1} {Step2} {Step3}", step1, step2, step3);
        return Ok(new { step1, step2, step3 });
    }

    /// <summary>Health check endpoint for Docker/load balancer probes.</summary>
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "healthy" });

    [Trace]
    private async Task<string> DoStep(string name, int delayMs)
    {
        _logger.LogDebug("Executing step {StepName} with delay {DelayMs}ms", name, delayMs);
        await Task.Delay(delayMs);
        return $"{name}-done";
    }
}
