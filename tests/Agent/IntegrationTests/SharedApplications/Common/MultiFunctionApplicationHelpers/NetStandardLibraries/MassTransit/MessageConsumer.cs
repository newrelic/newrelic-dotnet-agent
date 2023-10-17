// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransit
{
    public class MessageConsumer
        :
            IConsumer<Message>
    {
        readonly ILogger<MessageConsumer> _logger;
        public MessageConsumer(ILogger<MessageConsumer> logger)
        {
            _logger = logger;
        }

        public Task Consume(ConsumeContext<Message> context)
        {
            ConsoleMFLogger.Info($"Received message {context.Message.Text}");
            //_logger.LogInformation("Received Message: {Text}", context.Message.Text);
            return Task.CompletedTask;
        }
    }
}
