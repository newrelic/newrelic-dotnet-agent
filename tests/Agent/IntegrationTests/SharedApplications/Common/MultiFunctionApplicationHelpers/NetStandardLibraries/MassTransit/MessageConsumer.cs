// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MassTransit;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransit
{
    public class MessageConsumer : IConsumer<Message>
    {
        public Task Consume(ConsumeContext<Message> context)
        {
            ConsoleMFLogger.Info($"Received message {context.Message.Text}");
            return Task.CompletedTask;
        }
    }
}
