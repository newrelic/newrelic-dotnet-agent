// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NServiceBus;
using NServiceBusReceiver;

namespace NServiceBusReceiverHost
{
    public class MessageHandler : IHandleMessages<SampleNServiceBusMessage2>
    {
        public void Handle(SampleNServiceBusMessage2 message)
        {
            Console.WriteLine("Received {0} message with contents={1}", message.IsValid ? "Valid" : "Invalid", message.FooBar);

            if (!message.IsValid)
            {
                throw new Exception("An exception was thrown inside the NServiceBus Receive Handler!!!!");
            }
        }
    }
}
