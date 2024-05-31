// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers;
using NServiceBus;

#if !NET462

namespace NsbTests
{
    public class AsyncEventHandler :
    IHandleMessages<Event>
    {
        public async Task Handle(Event message, IMessageHandlerContext context)
        {
            ConsoleMFLogger.Info($"Async Event handler received message with Id {message.Id}.");
#pragma warning disable NSB0002 // Forward the 'CancellationToken' property of the context parameter to methods
            await Task.Delay(500);
#pragma warning restore NSB0002 // Forward the 'CancellationToken' property of the context parameter to methods
            ConsoleMFLogger.Info($"Async Event handler done delaying message with Id {message.Id}.");
            // Make sure segment/transaction ends
        }
    }
}

#endif
