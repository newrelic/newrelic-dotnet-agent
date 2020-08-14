// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

namespace AttributeInstrumentation
{
    class Program
    {
        [Option("port", Required = true)]
        public string Port { get; set; }

        static void Main(string[] args)
        {
            if (Parser.Default == null)
                throw new NullReferenceException("CommandLine.Parser.Default");

            var program = new Program();
            if (!Parser.Default.ParseArgumentsStrict(args, program))
                return;

            program.RealMain();
        }

        private void RealMain()
        {
            var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "app_server_wait_for_all_request_done_" + Port);
            CreatePidFile();

            // This test application exercises attribute-based custom instrumentation in a number of different use cases.
            // Each of the method calls below are meant to illustrate a different use case. They each create a transaction
            // (web or other) using the Transaction attribute and exercise other methods that have been decorated with the
            // Trace attribute.

            // Web trasactions (i.e. [Transaction(Web = true)])
            MakeWebTransaction();
            MakeWebTransactionWithCustomUri();

            // Other transactions (i.e. [Transaction] or [Transaction(Web = false)])
            MakeOtherTransaction();
            MakeOtherTransactionAsync().Wait();
            MakeOtherTransactionThenCallAsyncMethod();
            MakeOtherTransactionWithCallToNetStandardMethod();

            eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
        }

        private static void CreatePidFile()
        {
            var pid = Process.GetCurrentProcess().Id;
            var thisAssemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var pidFilePath = thisAssemblyPath + ".pid";
            var file = File.CreateText(pidFilePath);
            file.WriteLine(pid);
        }

        /// <summary>
        /// Synchronous method creating a Web transaction
        /// </summary>
        [NewRelic.Api.Agent.Transaction(Web = true)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void MakeWebTransaction()
        {
            DoSomeWork();
        }

        /// <summary>
        /// Synchronous method creating a Web transaction and utilizing SetTransactionUri
        /// </summary>
        [NewRelic.Api.Agent.Transaction(Web = true)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void MakeWebTransactionWithCustomUri()
        {
            var uri = new Uri("http://foo.bar.net/fizz/buzz");
            NewRelic.Api.Agent.NewRelic.SetTransactionUri(uri);
            DoSomeWork();
        }

        /// <summary>
        /// Synchronous method creating an Other transaction
        /// </summary>
        [NewRelic.Api.Agent.Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void MakeOtherTransaction()
        {
            DoSomeWork();
        }

        /// <summary>
        /// Async method creating an Other transaction
        /// </summary>
        /// <returns></returns>
        [NewRelic.Api.Agent.Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task MakeOtherTransactionAsync()
        {
            await DoSomeWorkAsync();
        }

        /// <summary>
        /// Synchronous method that calls async methods.
        /// This use case illustrates that this scenario is possible, but it is
        /// recommended that transactions involving async code start with an 
        /// async entry point to avoid potential pitfalls.
        /// </summary>
        [NewRelic.Api.Agent.Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void MakeOtherTransactionThenCallAsyncMethod()
        {
            DoSomeWorkAsync().Wait();
        }

        [NewRelic.Api.Agent.Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void MakeOtherTransactionWithCallToNetStandardMethod()
        {
            var myObj = new NetStandardClassLibrary.MyClass();
            myObj.MyMethodToBeInstrumented();
        }

        [NewRelic.Api.Agent.Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void DoSomeWork()
        {
        }

        [NewRelic.Api.Agent.Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<string> DoSomeWorkAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            return await DoSomeMoreWorkAsync();
        }

        [NewRelic.Api.Agent.Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<string> DoSomeMoreWorkAsync()
        {
            return await Task.FromResult("Some work.");
        }
    }
}
