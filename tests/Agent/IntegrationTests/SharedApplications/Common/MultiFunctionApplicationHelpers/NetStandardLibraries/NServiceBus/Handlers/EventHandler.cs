// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers;
using NewRelic.Api.Agent;
using NServiceBus;
using NServiceBus.Logging;

#if !NET462

namespace NServiceBusTests
{
    public class EventHandler :
    IHandleMessages<Event>
    {
        public Task Handle(Event message, IMessageHandlerContext context)
        {
            Logger.Info($"Event handler received message with Id {message.Id}.");
            return Task.CompletedTask;
        }
    }
}

#endif
