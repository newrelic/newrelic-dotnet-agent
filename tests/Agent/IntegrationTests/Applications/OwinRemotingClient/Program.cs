// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Microsoft.Owin.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CommandLine;
using System.Threading;

namespace OwinRemotingClient
{
    public class Program
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
            var baseAddress = string.Format(@"http://127.0.0.1:{0}/", Port);
            using (WebApp.Start<Startup>(baseAddress))
            {
                using (var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset,
                           "app_server_wait_for_all_request_done_" + Port.ToString()))
                {
                    CreatePidFile();
                    eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
                }
            }
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
