using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using CommandLine;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted
{
    public class Program
    {
        [Option("port", Required = true)]
        public String Port { get; set; }

        public static void Main(string[] args)
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
            var baseAddress = new Uri(String.Format(@"http://localhost:{0}/", Port));
            var serviceHost = ServiceHostFactory(baseAddress);
            using (new ServiceHostDisposer(serviceHost))
            {
                var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "app_server_wait_for_all_request_done_" + Port.ToString());
                CreatePidFile();
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
            }
        }

        private static ServiceHost ServiceHostFactory(Uri baseAddress)
        {
            if (baseAddress == null)
                throw new ArgumentNullException("baseAddress");

            var serviceHost = new ServiceHost(typeof(WcfService), baseAddress);
            if (serviceHost.Description == null)
                throw new NullReferenceException("serviceHost.Description");
            if (serviceHost.Description.Behaviors == null)
                throw new NullReferenceException("serviceHost.Description.Behaviors");

            var serviceMetadataBehavior = new ServiceMetadataBehavior
            {
                HttpGetEnabled = true,
                HttpsGetEnabled = true,
                MetadataExporter =
                {
                    PolicyVersion = PolicyVersion.Policy15
                }
            };
            serviceHost.Description.Behaviors.Add(serviceMetadataBehavior);

            return serviceHost;
        }

        private class ServiceHostDisposer : IDisposable
        {
            private readonly ServiceHost _serviceHost;

            public ServiceHostDisposer(ServiceHost serviceHost)
            {
                if (serviceHost == null)
                    throw new ArgumentNullException("serviceHost");

                _serviceHost = serviceHost;
                _serviceHost.Open();
            }

            public void Dispose()
            {
                _serviceHost.Close();
                (_serviceHost as IDisposable).Dispose();
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
