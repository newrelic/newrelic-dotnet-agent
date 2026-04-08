// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MassTransitTestApp.Controllers;

[ApiController]
[Route("inmemory")]
public class InMemoryController : ControllerBase
{
    private readonly ILogger<InMemoryController> _logger;
    private readonly IInMemoryBus _bus;

    public InMemoryController(ILogger<InMemoryController> logger, IInMemoryBus bus)
    {
        _logger = logger;
        _bus = bus;
    }

    [HttpGet("publish")]
    public async Task<string> Publish()
    {
        var message = new InMemoryMessage { Text = $"inmem-{Guid.NewGuid():N}" };
        await _bus.Publish(message);
        _logger.LogInformation("InMemory published: {Text}", message.Text);
        return "Complete";
    }
}
