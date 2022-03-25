// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers;
using NServiceBus;

#if !NET462

namespace NsbTests
{
    public class CommandHandler :
    IHandleMessages<Command>
    {
        public Task Handle(Command command, IMessageHandlerContext context)
        {
            ConsoleMFLogger.Info($"Command handler received message with Id {command.Id}.");
            return Task.CompletedTask;
        }
    }
}

#endif
