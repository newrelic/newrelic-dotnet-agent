// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MassTransitTestApp.Controllers;

[ApiController]
[Route("kafka")]
public class KafkaController : ControllerBase
{
    private readonly ILogger<KafkaController> _logger;
    private readonly ITopicProducer<KafkaMessage> _producer;

    public KafkaController(ILogger<KafkaController> logger, ITopicProducer<KafkaMessage> producer)
    {
        _logger = logger;
        _producer = producer;
    }

    [HttpGet("produce")]
    public async Task<string> Produce()
    {
        var message = new KafkaMessage { Text = $"kafka-{Guid.NewGuid():N}" };
        await _producer.Produce(message);
        _logger.LogInformation("Kafka produced: {Text}", message.Text);
        return "Complete";
    }

    [HttpGet("bootstrap_server")]
    public string GetBootstrapServer() => Program.GetKafkaBootstrapServer();
}
