using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using CommandLine;
using JetBrains.Annotations;

namespace NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor
{
    public class Program
    {
        [Option("port", Required = true)]
        [NotNull]
        public String Port { get; set; }

        static void Main(string[] args)
        {
            RealMain(args);
            Thread.Sleep(1000); //needed for OtherTransaction test
        }

        static void RealMain(string[] args)
        {
            if (Parser.Default == null)
                throw new NullReferenceException("CommandLine.Parser.Default");

            var program = new Program();
            if (!Parser.Default.ParseArgumentsStrict(args, program))
                return;

            // Create handle that RemoteApplication expects
            new EventWaitHandle(false, EventResetMode.ManualReset, "app_server_wait_for_all_request_done_" + program.Port);

            CreatePidFile();

            Api.Agent.NewRelic.StartAgent();

            SomeSlowMethod();

            Api.Agent.NewRelic.RecordMetric("MyMetric", 3.14159F);
            Api.Agent.NewRelic.NoticeError(new Exception("Rawr!"));

            var errorAttributes = new Dictionary<string, string>
            {
                {"hey", "dude"},
                {"faz", "baz"},
            };
            Api.Agent.NewRelic.NoticeError(new Exception("Rawr!"), errorAttributes);

        }

        private static void SomeSlowMethod()
        {
            var stuff = string.Empty;
            Api.Agent.NewRelic.AddCustomParameter("test", "test");
            Thread.Sleep(2000); //needed for OtherTransaction test
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
