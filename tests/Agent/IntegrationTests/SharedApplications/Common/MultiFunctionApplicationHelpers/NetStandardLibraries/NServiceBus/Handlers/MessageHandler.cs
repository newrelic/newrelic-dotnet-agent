// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers;
using NServiceBus;
using NServiceBus.Logging;

namespace NServiceBusTests
{
    public class MessageHandler :
    IHandleMessages<Message>
    {
        public Task Handle(Message message, IMessageHandlerContext context)
        {
            Logger.Info($"Message handler received message with Id {message.Id}.");
            return Task.CompletedTask;
        }
    }
}
