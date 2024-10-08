// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET462

using System;
using System.Threading;
using NServiceBus;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5

{
    public class SampleNServiceBusMessage : ICommand
    {
        public int Id { get; private set; }
        public string FooBar { get; private set; }

        public SampleNServiceBusMessage(int id, string fooBar)
        {
            Thread.Sleep(250);
            Id = id;
            FooBar = fooBar;
        }
    }
}

#endif
