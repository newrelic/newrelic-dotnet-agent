// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers;
using NServiceBus;

#if !NET462

namespace NServiceBusTests
{
    public class CommandHandler :
    IHandleMessages<Command>
    {
        public Task Handle(Command command, IMessageHandlerContext context)
        {
            Logger.Info($"Command handler received message with Id {command.Id}.");
            return Task.CompletedTask;
        }
    }

    public class ThrowingCommandHandler :
    IHandleMessages<Command>
    {
        public Task Handle(Command command, IMessageHandlerContext context)
        {
            Logger.Info($"Throwing Command handler received message with Id {command.Id}.");
            throw new System.Exception("Oh noez! Invalid message");
        }
    }

    public class AsyncCommandHandler :
    IHandleMessages<Command>
    {
        public async Task Handle(Command command, IMessageHandlerContext context)
        {
            Logger.Info($"Async Command handler received message with Id {command.Id}.");
            await Task.Delay(500);
            Logger.Info($"Async Command handler done delaying message with Id {command.Id}.");
            // Make sure segment/transaction ends
        }
    }
}

#endif
