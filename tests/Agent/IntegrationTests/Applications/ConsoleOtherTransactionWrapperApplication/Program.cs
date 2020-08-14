// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Api.Agent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleOtherTransactionWrapperApplication
{
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
        private static void InnerInstrumentedMethod()
        {
            Thread.Sleep(TimeSpan.FromSeconds(_delaySeconds));
        }


    }
}
