using System;
using System.Diagnostics.Contracts;
using System.IO;
using CommandLine;

namespace HostedWebCore
{
    static class Program
    {
        private static void Log(String format, params object[] values)
        {
            String prefix = String.Format("[{0} {1}-{2}] HostedWebCore: ", DateTime.Now,
                    System.Diagnostics.Process.GetCurrentProcess().Id,
                    System.Threading.Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine(String.Format(prefix + format, values));
        }

        private static void Main(String[] args)
        {
            string msg = "Firing up...args: " + string.Join(", ", args);
            // Must replace curlies else e.g. '{0}' will cause an exception in Log()
            Log(msg.Replace('{', '[').Replace('}', ']'));
            Log("Starting directory: " + Directory.GetCurrentDirectory());
            Log("Environment Variables: " + String.Join(";", Environment.GetEnvironmentVariables()));

            if (Parser.Default == null)
                throw new NullReferenceException("CommandLine.Parser.Default");

            var options = new Options();
            if (!Parser.Default.ParseArgumentsStrict(args, options))
                return;

            Contract.Assume(options.Port != null);

            var hostedWebCore = new HostedWebCore(options.Port);
            Log("Starting server...");
            hostedWebCore.Run();
            Log("Done.");
        }
    }
}
