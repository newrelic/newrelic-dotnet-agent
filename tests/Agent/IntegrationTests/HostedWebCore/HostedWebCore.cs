// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace HostedWebCore
{
    public class HostedWebCore
    {
        private const int ServerTimeoutShutdownMinutes = 5;

        private readonly string _port;

        private static string AssemblyDirectory
        {
            get
            {
                Contract.Ensures(Contract.Result<string>() != null);

                var codeBase = Assembly.GetExecutingAssembly().CodeBase;
                var uri = new UriBuilder(codeBase);
                var path = Uri.UnescapeDataString(uri.Uri.LocalPath);
                return Path.GetDirectoryName(path);
            }
        }

        private static string ApplicationHostConfigFilePath
        {
            get
            {
                Contract.Ensures(Contract.Result<string>() != null);
                return AssemblyDirectory + @"\applicationHost.config";
            }
        }

        [ContractInvariantMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Required for code contracts.")]
        private void ObjectInvariant()
        {
            Contract.Invariant(_port != null);
        }

        public HostedWebCore(string port)
        {
            Contract.Requires(port != null);

            _port = port;
        }

        public void Run()
        {
            StartWebServer();
            //The HWC creates this shutdown event and waits for the test runner to set so that it can shutdown.  
            using (var eventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset,
                       "app_server_wait_for_all_request_done_" + _port.ToString()))
            {
                CreatePidFile();
                eventWaitHandle.WaitOne(TimeSpan.FromMinutes(ServerTimeoutShutdownMinutes));
            }
        }

        private void StartWebServer()
        {
            int maxRetries = 3;
            int curRetry = 0;

            while (true)
            {
                try
                {
                    var hResult = NativeMethods.WebCoreActivate(ApplicationHostConfigFilePath, null, @".NET Agent Integration Test Web Host");
                    Marshal.ThrowExceptionForHR(hResult);

                    return; // success
                }
                catch
                {
                    if (curRetry++ < maxRetries)
                    {
                        Thread.Sleep(500);
                    }
                    else
                        throw; // all retries failed
                }
            }
        }

        private static void CreatePidFile()
        {
            var pid = Process.GetCurrentProcess().Id;
            var thisAssemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var pidFilePath = thisAssemblyPath + ".pid";
            File.WriteAllText(pidFilePath, pid.ToString(CultureInfo.InvariantCulture));
        }
    }
}
