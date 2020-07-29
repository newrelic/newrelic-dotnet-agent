using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;
using CommandLine;
using OwinRemotingShared;

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
            var serverProviderTcp = new BinaryServerFormatterSinkProvider();
            serverProviderTcp.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
            var clientProviderTcp = new BinaryClientFormatterSinkProvider();
            var propertiesTcp = new System.Collections.Hashtable();
            propertiesTcp["port"] = 9001;

            var tcpChannel = new TcpChannel(propertiesTcp, clientProviderTcp, serverProviderTcp);
            ChannelServices.RegisterChannel(tcpChannel, false);

            var serverProviderHttp = new SoapServerFormatterSinkProvider();
            serverProviderHttp.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
            var clientProviderHttp = new SoapClientFormatterSinkProvider();
            var propertiesHttp = new System.Collections.Hashtable();
            propertiesHttp["port"] = 9002;

            var httpChannel = new HttpChannel(propertiesHttp, clientProviderHttp, serverProviderHttp);
            ChannelServices.RegisterChannel(httpChannel, false);

            RemotingConfiguration.RegisterWellKnownServiceType(typeof(MyMarshalByRefClass), "GetObject", WellKnownObjectMode.SingleCall);

            var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "app_server_wait_for_all_request_done_" + Port.ToString());
            eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
        }
    }
}
