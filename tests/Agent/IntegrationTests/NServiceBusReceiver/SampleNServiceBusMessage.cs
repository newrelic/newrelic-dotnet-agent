// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Threading;
using NServiceBus;

namespace NServiceBusReceiver

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
