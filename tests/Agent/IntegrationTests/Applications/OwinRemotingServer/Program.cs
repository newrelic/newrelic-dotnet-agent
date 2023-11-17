// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using CommandLine;
using OwinRemotingShared;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace OwinRemotingServer
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
            var serverProviderTcp = new BinaryServerFormatterSinkProvider { TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full };
            var clientProviderTcp = new BinaryClientFormatterSinkProvider();
            var propertiesTcp = new System.Collections.Hashtable { ["port"] = 7878, ["bindTo"] = "127.0.0.1" };

            var tcpChannel = new TcpChannel(propertiesTcp, clientProviderTcp, serverProviderTcp);
            ChannelServices.RegisterChannel(tcpChannel, false);

            var serverProviderHttp = new SoapServerFormatterSinkProvider { TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full };
            var clientProviderHttp = new SoapClientFormatterSinkProvider();
            var propertiesHttp = new System.Collections.Hashtable { ["port"] = 7879, ["bindTo"] = "127.0.0.1" };

            var httpChannel = new HttpChannel(propertiesHttp, clientProviderHttp, serverProviderHttp);
            ChannelServices.RegisterChannel(httpChannel, false);

            RemotingConfiguration.RegisterWellKnownServiceType(typeof(MyMarshalByRefClass), "GetObject",
                WellKnownObjectMode.SingleCall);

            using (var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset,
                       "app_server_wait_for_all_request_done_" + Port.ToString()))
            {
                CreatePidFile();
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
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
