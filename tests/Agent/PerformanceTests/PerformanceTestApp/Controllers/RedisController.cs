// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace PerformanceTestApp.Controllers;

[ApiController]
[Route("[controller]")]
public class RedisController : ControllerBase
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisController> _logger;

    public RedisController(IConnectionMultiplexer multiplexer, ILogger<RedisController> logger)
    {
        _multiplexer = multiplexer;
        _logger = logger;
    }

    [HttpGet("crud")]
    public async Task<IActionResult> Crud()
    {
        var db = _multiplexer.GetDatabase();
        var key = $"perf:{Guid.NewGuid()}";
        var value = Guid.NewGuid().ToString();

        await db.StringSetAsync(key, value);
        _logger.LogInformation("Set key {Key} with value {Value}", key, value);

        var retrieved = await db.StringGetAsync(key);
        _logger.LogInformation("Got key {Key}: {Value}", key, (string?)retrieved);

        await db.StringSetAsync(key, "updated");
        _logger.LogInformation("Updated key {Key}", key);

        await db.KeyDeleteAsync(key);
        _logger.LogInformation("Deleted key {Key}", key);

        return Ok(new { key });
    }
}
