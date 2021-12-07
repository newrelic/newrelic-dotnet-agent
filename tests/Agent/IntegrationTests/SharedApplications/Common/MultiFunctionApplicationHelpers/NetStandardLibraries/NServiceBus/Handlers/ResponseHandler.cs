// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using MultiFunctionApplicationHelpers;
using NServiceBus;

#if !NET462

namespace NServiceBusTests
{
    public class ResponseHandler :
    IHandleMessages<Response>
    {
        public Task Handle(Response message, IMessageHandlerContext context)
        {
            Logger.Info($"Response handler received message with Id {message.Id}.");
            return Task.CompletedTask;
        }
    }
}

#endif
