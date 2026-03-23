// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PerformanceTestApp.Controllers;

[ApiController]
[Route("[controller]")]
public class RabbitMqController : ControllerBase
{
    private const string QueueName = "perf";

    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqController> _logger;

    public RabbitMqController(IConnection connection, ILogger<RabbitMqController> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    [HttpPost("publish")]
    public IActionResult Publish()
    {
        var message = Guid.NewGuid().ToString();

        using var channel = _connection.CreateModel();
        channel.BasicPublish(exchange: "", routingKey: QueueName, basicProperties: null,
            body: Encoding.UTF8.GetBytes(message));

        _logger.LogInformation("Published message {Message} to queue {Queue}", message, QueueName);
        return Ok(new { message });
    }

    [HttpGet("consume")]
    public async Task<IActionResult> Consume()
    {
        using var channel = _connection.CreateModel();

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new EventingBasicConsumer(channel);
        string consumerTag = null!;

        consumer.Received += (_, args) =>
        {
            var msg = Encoding.UTF8.GetString(args.Body.ToArray());
            tcs.TrySetResult(msg);
        };

        consumerTag = channel.BasicConsume(QueueName, autoAck: true, consumer: consumer);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        channel.BasicCancel(consumerTag);

        if (completed != tcs.Task)
        {
            _logger.LogInformation("No messages available in queue {Queue}", QueueName);
            return Ok(new { message = (string?)null });
        }

        var message = await tcs.Task;
        _logger.LogInformation("Consumed message {Message} from queue {Queue}", message, QueueName);
        return Ok(new { message });
    }
}
