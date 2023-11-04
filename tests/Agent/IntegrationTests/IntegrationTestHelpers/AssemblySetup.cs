// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class AssemblySetUp
    {
        static AssemblySetUp()
        {
            Contract.ContractFailed += new EventHandler<ContractFailedEventArgs>((sender, eventArgs) =>
            {
                eventArgs.SetHandled();
                Assert.Fail(string.Format("{0}: {1} {2}", eventArgs.FailureKind, eventArgs.Message, eventArgs.Condition));
            });
        }

        // when called, will cause this static class to be instantiated which will cause the constructor to be called which will hook up the global contract failure event handler.  Basically, this is just a global function that needs to be called once when the application starts up.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public static void TouchMe() { }
    }
}
