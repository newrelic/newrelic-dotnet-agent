// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers;
using NServiceBus;

#if !NET462

namespace NsbTests
{
    public class ThrowingCommandHandler :
    IHandleMessages<Command>
    {
        public Task Handle(Command command, IMessageHandlerContext context)
        {
            ConsoleMFLogger.Info($"Throwing Command handler received message with Id {command.Id}.");
            throw new System.Exception("Oh noez! Invalid message");
        }
    }
}

#endif
