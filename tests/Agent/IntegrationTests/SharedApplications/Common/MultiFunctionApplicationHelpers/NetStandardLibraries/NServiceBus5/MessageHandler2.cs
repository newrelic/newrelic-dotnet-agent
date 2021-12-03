// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET462

using System;
using NServiceBus;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5
{
    public class MessageHandler2 : IHandleMessages<SampleNServiceBusMessage2>
    {
        public void Handle(SampleNServiceBusMessage2 message)
        {
            Logger.Info("Received {0} message with contents={1}", message.IsValid ? "Valid" : "Invalid", message.FooBar);

            if (!message.IsValid)
            {
                throw new Exception("An exception was thrown inside the NServiceBus Receive Handler!!!!");
            }
        }
    }
}

#endif
