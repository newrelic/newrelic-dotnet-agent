// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET462

using System;
using NServiceBus;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5
{
    public class MessageHandler : IHandleMessages<SampleNServiceBusMessage>
    {
        public void Handle(SampleNServiceBusMessage message)
        {
            Logger.Info("Received message with contents={1}", message.FooBar);
        }
    }
}

#endif
