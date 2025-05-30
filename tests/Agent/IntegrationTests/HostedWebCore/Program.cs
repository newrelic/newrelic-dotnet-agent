// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections;
using System.Diagnostics.Contracts;
using CommandLine;
using System.IO;
using System.Text;

namespace HostedWebCore
{
    static class Program
    {
        private static void Log(string format)
        {
            string prefix = string.Format("[{0} {1}-{2}] HostedWebCore: ", DateTime.Now,
                    System.Diagnostics.Process.GetCurrentProcess().Id,
                    System.Threading.Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine(prefix + format);
        }

        private static void Main(string[] args)
        {
            string msg = "Firing up...args: " + string.Join(", ", args);
            Log("Firing up...args: " + string.Join(", ", args));
            Log("Starting directory: " + Directory.GetCurrentDirectory());

            var environmentVariables = new StringBuilder();
            foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
            {
                // strip newlines in the environment variable value - otherwise our log parsing may fail
                var valueStr = de.Value?.ToString().Replace("\r", string.Empty).Replace("\n", string.Empty);
                environmentVariables.Append($"  {de.Key} = {valueStr}; ");
            }
            Log("Environment Variables: " + environmentVariables.ToString());

            if (Parser.Default == null)
                throw new NullReferenceException("CommandLine.Parser.Default");

            var options = new Options();
            if (!Parser.Default.ParseArgumentsStrict(args, options))
                return;

            Contract.Assume(options.Port != null);

            try
            {
                var hostedWebCore = new HostedWebCore(options.Port);
                Log("Starting server...");
                hostedWebCore.Run();
                Log("Done.");
            }
            catch (DllNotFoundException ex)
            {
                Log($"HostedWebCore.exe failed: Check that the Hostable Web Core Windows feature is installed.: {ex}");
                Environment.Exit(4);
            }
            catch (FileNotFoundException ex)
            {
                Log($"HostedWebCore.exe failed: Running HostedWebCore.exe requires certain IIS components to be installed.: {ex}");
                Environment.Exit(3);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log($"HostedWebCore.exe failed: This application must be run with administrator privileges.: {ex}");
                Environment.Exit(2);
            }
            catch (Exception ex)
            {
                Log($"HostedWebCore.exe failed: Reason unknown.: {ex}");
                Environment.Exit(1);
            }
        }
    }
}
