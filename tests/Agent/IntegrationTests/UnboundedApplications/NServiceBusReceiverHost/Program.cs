/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NServiceBus;

namespace NServiceBusReceiverHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Starting up {System.AppDomain.CurrentDomain.FriendlyName}");
            Console.WriteLine($"Process Id: {Process.GetCurrentProcess().Id}");
            Console.WriteLine($"Executing with arguments: {string.Join(" ", args)}");

            // Note: the only reason this test app needs a 'port' argument is to register a unique
            // EventWaitHandle name that the test framework can use to signal this app to shut down
            var portArg = args.FirstOrDefault(x => x.StartsWith("--port="));
            if (portArg == null)
            {
                throw new ArgumentException("Argument --port={port} must be supplied");
            }
            var port = portArg.Split('=')[1];


            var busConfig = new BusConfiguration();
            busConfig.UsePersistence<InMemoryPersistence>();
            busConfig.UseTransport<MsmqTransport>(); // maybe not necessary


            var startableBus = Bus.Create(busConfig);
            var bus = startableBus.Start();

            var ewhName = "app_server_wait_for_all_request_done_" + port;
            Console.WriteLine($"Creating event wait handle with name={"app_server_wait_for_all_request_done_" + port}");
            var ewh = new EventWaitHandle(false, EventResetMode.ManualReset, ewhName);
            CreatePidFile();
            ewh.WaitOne(TimeSpan.FromMinutes(5));

            bus.Dispose();
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
