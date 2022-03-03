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
            await Task.Delay(500);
            ConsoleMFLogger.Info($"Async Event handler done delaying message with Id {message.Id}.");
            // Make sure segment/transaction ends
        }
    }
}

#endif
