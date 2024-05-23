// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers;
using NServiceBus;

#if !NET462

namespace NsbTests
{
    public class AsyncCommandHandler :
    IHandleMessages<Command>
    {
        public async Task Handle(Command command, IMessageHandlerContext context)
        {
            ConsoleMFLogger.Info($"Async Command handler received message with Id {command.Id}.");
#pragma warning disable NSB0002 // Forward the 'CancellationToken' property of the context parameter to methods
            await Task.Delay(500);
#pragma warning restore NSB0002 // Forward the 'CancellationToken' property of the context parameter to methods
            ConsoleMFLogger.Info($"Async Command handler done delaying message with Id {command.Id}.");
            // Make sure segment/transaction ends
        }
    }
}

#endif
