// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Internal
{
    [Library]
    internal static class AttributeInstrumentation
    {
        /// <summary>
        /// Synchronous method creating a Web transaction
        /// </summary>
        [LibraryMethod]
        [Transaction(Web = true)]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void MakeWebTransaction()
        {
            DoSomeWork();
        }

        /// <summary>
        /// Synchronous method creating a Web transaction and utilizing SetTransactionUri
        /// </summary>
        [LibraryMethod]
        [Transaction(Web = true)]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void MakeWebTransactionWithCustomUri()
        {
            var uri = new Uri("http://foo.bar.net/fizz/buzz");
            NewRelic.Api.Agent.NewRelic.SetTransactionUri(uri);
            DoSomeWork();
        }

        /// <summary>
        /// Synchronous method creating an Other transaction
        /// </summary>
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void MakeOtherTransaction()
        {
            DoSomeWork();
        }

        /// <summary>
        /// Async method creating an Other transaction
        /// </summary>
        /// <returns></returns>
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task MakeOtherTransactionAsync()
        {
            await DoSomeWorkAsync();
        }

        /// <summary>
        /// Synchronous method that calls async methods.
        /// This use case illustrates that this scenario is possible, but it is
        /// recommended that transactions involving async code start with an 
        /// async entry point to avoid potential pitfalls.
        /// </summary>
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void MakeOtherTransactionThenCallAsyncMethod()
        {
            DoSomeWorkAsync().Wait();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void MakeOtherTransactionWithCallToNetStandardMethod()
        {
            var myObj = new NetStandardTestLibrary.MyClass();
            myObj.MyMethodToBeInstrumented();
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static void DoSomeWork()
        {
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static async Task<string> DoSomeWorkAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            return await DoSomeMoreWorkAsync();
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static async Task<string> DoSomeMoreWorkAsync()
        {
            return await Task.FromResult("Some work.");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task<string> MakeOtherTransactionWithThreadedCallToInstrumentedMethod()
        {
            return await Task.Run(SpanOrTransactionBasedOnConfig);

        }

        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static async Task<string> SpanOrTransactionBasedOnConfig()
        {
            return await Task.FromResult("New Transaction or span based on config.");
        }
    }
}
