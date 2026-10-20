// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MassTransitTestApp.Controllers;

[ApiController]
[Route("sqs")]
public class SqsController : ControllerBase
{
    private readonly ILogger<SqsController> _logger;
    private readonly ISqsBus _bus;

    public SqsController(ILogger<SqsController> logger, ISqsBus bus)
    {
        _logger = logger;
        _bus = bus;
    }

    [HttpGet("send")]
    public async Task<string> Send()
    {
        var queueName = Program.GetSqsQueueName();
        var endpoint = await _bus.GetSendEndpoint(new Uri($"queue:{queueName}"));
        var message = new SqsMessage { Text = $"sqs-send-{Guid.NewGuid():N}" };
        await endpoint.Send(message);
        _logger.LogInformation("SQS sent: {Text}", message.Text);
        return "Complete";
    }
}
