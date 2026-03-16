// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace PerformanceTestApp.Controllers;

[ApiController]
[Route("[controller]")]
public class HomeController : ControllerBase
{
    private readonly ILogger<HomeController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>Simple endpoint that returns quickly - baseline transaction overhead.</summary>
    [HttpGet("simple")]
    public IActionResult Simple()
    {
        return Ok(new { message = "ok", timestamp = DateTime.UtcNow });
    }

    /// <summary>CPU-bound endpoint that exercises agent overhead under computation.</summary>
    [HttpGet("cpu")]
    public IActionResult Cpu([FromQuery] int iterations = 1000)
    {
        var result = 0L;
        for (var i = 0; i < iterations; i++)
            result += i * i;

        return Ok(new { result, iterations });
    }

    /// <summary>Async I/O simulation endpoint that exercises async context propagation.</summary>
    [HttpGet("io")]
    public async Task<IActionResult> Io([FromQuery] int delayMs = 10)
    {
        await Task.Delay(delayMs);
        return Ok(new { delayMs, completed = true });
    }

    /// <summary>Nested async calls that simulate a multi-layer service.</summary>
    [HttpGet("nested")]
    public async Task<IActionResult> Nested()
    {
        var step1 = await DoStep("step1", 5);
        var step2 = await DoStep("step2", 5);
        var step3 = await DoStep("step3", 5);

        return Ok(new { step1, step2, step3 });
    }

    /// <summary>Endpoint that generates a collection - exercises serialization path.</summary>
    [HttpGet("collection")]
    public IActionResult Collection([FromQuery] int count = 20)
    {
        var items = Enumerable.Range(1, count).Select(i => new
        {
            id = i,
            name = $"item-{i}",
            value = i * 3.14
        });

        return Ok(items);
    }

    /// <summary>Health check endpoint for Docker/load balancer probes.</summary>
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "healthy" });

    private static async Task<string> DoStep(string name, int delayMs)
    {
        await Task.Delay(delayMs);
        return $"{name}-done";
    }
}
