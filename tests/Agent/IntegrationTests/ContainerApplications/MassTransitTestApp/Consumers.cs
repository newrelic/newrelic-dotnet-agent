// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MassTransitTestApp;

public class KafkaMessageConsumer : IConsumer<KafkaMessage>
{
    private readonly ILogger<KafkaMessageConsumer> _logger;

    public KafkaMessageConsumer(ILogger<KafkaMessageConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<KafkaMessage> context)
    {
        _logger.LogInformation("Kafka consumed: {Text}", context.Message.Text);
        return Task.CompletedTask;
    }
}

public class RabbitMqMessageConsumer : IConsumer<RabbitMqMessage>
{
    private readonly ILogger<RabbitMqMessageConsumer> _logger;

    public RabbitMqMessageConsumer(ILogger<RabbitMqMessageConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<RabbitMqMessage> context)
    {
        _logger.LogInformation("RabbitMQ consumed: {Text}", context.Message.Text);
        return Task.CompletedTask;
    }
}

public class InMemoryMessageConsumer : IConsumer<InMemoryMessage>
{
    private readonly ILogger<InMemoryMessageConsumer> _logger;

    public InMemoryMessageConsumer(ILogger<InMemoryMessageConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InMemoryMessage> context)
    {
        _logger.LogInformation("InMemory consumed: {Text}", context.Message.Text);
        return Task.CompletedTask;
    }
}
