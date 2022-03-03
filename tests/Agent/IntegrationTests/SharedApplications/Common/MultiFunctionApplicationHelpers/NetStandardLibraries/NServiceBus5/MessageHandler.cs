// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET462

using System;
using NServiceBus;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5
{
    public class MessageHandler : IHandleMessages<SampleNServiceBusMessage2>
    {
        public void Handle(SampleNServiceBusMessage2 message)
        {
            var valid = message.IsValid ? "Valid" : "Invalid";
            ConsoleMFLogger.Info($"Received {valid} message with contents={message.FooBar}");

            if (!message.IsValid)
            {
                ConsoleMFLogger.Info("Message was invalid, throwing an exception!");
                throw new Exception("An exception was thrown inside the NServiceBus Receive Handler!!!!");
            }
        }
    }
}

#endif
