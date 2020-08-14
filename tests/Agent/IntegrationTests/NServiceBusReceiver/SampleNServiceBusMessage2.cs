// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Threading;
using NServiceBus;

namespace NServiceBusReceiver
{
    public class SampleNServiceBusMessage2 : ICommand
    {
        public int Id { get; private set; }
        public string FooBar { get; private set; }
        public bool IsValid { get; private set; }

        public SampleNServiceBusMessage2(int id, string fooBar, bool isValid = true)
        {
            Thread.Sleep(250);
            Id = id;
            FooBar = fooBar;
            IsValid = isValid;
        }
    }
}
