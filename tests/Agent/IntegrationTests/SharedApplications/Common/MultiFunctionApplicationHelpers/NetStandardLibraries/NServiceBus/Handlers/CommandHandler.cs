// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers;
using NServiceBus;
using NServiceBus.Logging;

namespace NServiceBusTests
{
    public class CommandHandler :
    IHandleMessages<Command>
    {
        public Task Handle(Command message, IMessageHandlerContext context)
        {
            var response = new Response();
            Logger.Info($"Command handler received message with Id {message.Id}, responding with Id {response.Id}.");
            context.Reply(response).Wait();
            return Task.CompletedTask;
        }
    }
}
