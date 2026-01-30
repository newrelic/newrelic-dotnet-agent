// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NewRelic.Api.Agent;

namespace ConsoleOtherTransactionWrapperApplication;

class Program
{

    private const int _delaySeconds = 2;

    static void Main(string[] args)
    {
        OuterInstrumentedMethod();
    }

    /// <summary>
    /// This is the first instrumented method, it will create the transaction and it is responsible
    /// for recording the response time at its end.
    /// </summary>
    [Transaction]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OuterInstrumentedMethod()
    {
        Thread.Sleep(TimeSpan.FromSeconds(_delaySeconds));
        InnerInstrumentedMethod();
    }

    /// <summary>
    /// This method should invoke the other transaction wrapper.  When it ends, it should not update
    /// the response time of the transaction because it was not the one that created the transaction.
    /// </summary>
    [Transaction]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InnerInstrumentedMethod()
    {
        Thread.Sleep(TimeSpan.FromSeconds(_delaySeconds));
    }


}
