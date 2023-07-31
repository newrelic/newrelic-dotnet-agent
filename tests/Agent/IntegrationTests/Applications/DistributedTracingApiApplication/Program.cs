// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using CommandLine;
using NewRelic.Api.Agent;

namespace NewRelic.Agent.IntegrationTests.Applications.DistributedTracingApiApplication
{
    public class Program
    {
        [Option("port", Required = true)]
        public string Port { get; set; }

        private static IAgent _agent = null;

        private static readonly List<KeyValuePair<string, string>> _carrier = new List<KeyValuePair<string, string>>();

        private static readonly Action<List<KeyValuePair<string, string>>, string, string> _setHeaders = new Action<List<KeyValuePair<string, string>>, string, string>((carrier, key, value) =>
        {
            carrier.Add(new KeyValuePair<string, string>(key, value));
        });

        private static IList<string> GetHeaders(IEnumerable<KeyValuePair<string, string>> carrier, string key)
        {
            var headerValues = new List<string>();

            foreach (var item in carrier)
            {
                if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    headerValues.Add(item.Value);
                }
            }

            return headerValues;

        }

        static void Main(string[] args)
        {
            if (Parser.Default == null)
                throw new NullReferenceException("CommandLine.Parser.Default");

            var program = new Program();
            if (!Parser.Default.ParseArgumentsStrict(args, program))
                return;

            // Create handle that RemoteApplication expects
            Console.WriteLine("PID {0} Creating EventWaitHandle {1}", Process.GetCurrentProcess().Id, "app_server_wait_for_all_request_done_" + program.Port);
            using (var eventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset,
                       "app_server_wait_for_all_request_done_" + program.Port))
            {

                CreatePidFile();


                _agent = Api.Agent.NewRelic.GetAgent();

                if (Array.IndexOf(args, "w3c") >= 0)
                {
                    CallInsertDTHeaders();
                    CallAcceptDTHeaders();
                }
                else
                {
                    throw new NotImplementedException();
                }

                // wait for the test harness to tell us to shut down
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
            }
        }

        [Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CallInsertDTHeaders()
        {
            var currentTransaction = _agent.CurrentTransaction;
            currentTransaction.InsertDistributedTraceHeaders(_carrier, _setHeaders);
        }

        [Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CallAcceptDTHeaders()
        {
            var currentTransaction = _agent.CurrentTransaction;
            currentTransaction.AcceptDistributedTraceHeaders(_carrier, GetHeaders, TransportType.HTTP);
        }

        private static void CreatePidFile()
        {
            var pid = Process.GetCurrentProcess().Id;
            var thisAssemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var pidFilePath = thisAssemblyPath + ".pid";
            var file = File.CreateText(pidFilePath);
            file.WriteLine(pid);
        }
    }
}
