// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NET462

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Models;
using NServiceBus;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Handlers;

public class EventHandler :
IHandleMessages<Event>
{
    public Task Handle(Event message, IMessageHandlerContext context)
    {
        ConsoleMFLogger.Info($"Event handler received message with Id {message.Id}.");
        return Task.CompletedTask;
    }
}
#endif
