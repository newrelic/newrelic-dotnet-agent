using System;
using System.Threading;
using CommandLine;
using JetBrains.Annotations;
using Microsoft.Owin.Hosting;

namespace OwinRemotingClient
{
    public class Program
    {
        [Option("port", Required = true)]
        [NotNull]
        public String Port { get; set; }

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
            var baseAddress = String.Format(@"http://*:{0}/", Port);
            using (WebApp.Start<Startup>(baseAddress))
            {
                var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "app_server_wait_for_all_request_done_" + Port.ToString());
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
            }
        }
    }
}
