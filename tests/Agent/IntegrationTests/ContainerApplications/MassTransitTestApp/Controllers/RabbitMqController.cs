// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MassTransitTestApp.Controllers;

[ApiController]
[Route("rabbitmq")]
public class RabbitMqController : ControllerBase
{
    private readonly ILogger<RabbitMqController> _logger;
    private readonly IBus _bus;

    public RabbitMqController(ILogger<RabbitMqController> logger, IBus bus)
    {
        _logger = logger;
        _bus = bus;
    }

    [HttpGet("publish")]
    public async Task<string> Publish()
    {
        var message = new RabbitMqMessage { Text = $"rmq-pub-{Guid.NewGuid():N}" };
        await _bus.Publish(message);
        _logger.LogInformation("RabbitMQ published: {Text}", message.Text);
        return "Complete";
    }

    [HttpGet("send")]
    public async Task<string> Send()
    {
        var queueName = Program.GetRabbitMqQueueName();
        var endpoint = await _bus.GetSendEndpoint(new Uri($"queue:{queueName}"));
        var message = new RabbitMqMessage { Text = $"rmq-send-{Guid.NewGuid():N}" };
        await endpoint.Send(message);
        _logger.LogInformation("RabbitMQ sent: {Text}", message.Text);
        return "Complete";
    }
}
