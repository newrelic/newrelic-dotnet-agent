// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using FunctionalTests.Helpers;

namespace FunctionalTests
{
    /// <summary>
    /// Class for common items related to functional tests.
    /// </summary>
    public class Common
    {
        public static Dictionary<String, TestServer> _testServerContainer = new Dictionary<string, TestServer>();
        public static Dictionary<String, TestServer> TestServerContainer { get { return _testServerContainer; } }

        private static String _stagingAccountId;
        public static String StagingAccountId
        {
            get
            {
                _stagingAccountId = _stagingAccountId ?? "273070";
                return _stagingAccountId;
            }
        }

        #region Helpers
        public static T Impersonate<T>(Func<T> method)
        {
            using (new ImpersonateUser("Administrator", null, "!4maline!"))
            {
                return method();
            }
        }

        public static void Impersonate(Action method)
        {
            using (new ImpersonateUser("Administrator", null, "!4maline!"))
            {
                method();
            }
        }
        #endregion Helpers

        /// <summary>
        /// Checks if the specified file is loaded into the specified process.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="processName">The name of the process.</param>
        /// <returns>Boolean representing if the file is loaded in the process.</returns>
        public static bool FileLoadedInProcess(ManagementScope mgmtScope, String fileName, String processName)
        {
            var handle = int.MinValue;

            using (var collection = new ManagementObjectSearcher(mgmtScope, new ObjectQuery($"SELECT Handle FROM CIM_Process WHERE Name='{processName}'")).Get())
            {
                foreach (ManagementObject item in collection)
                {
                    handle = Convert.ToInt16(item["Handle"]);
                    using (var collection2 = new ManagementObjectSearcher(mgmtScope, new ObjectQuery("ASSOCIATORS OF {Win32_Process.Handle='" + handle + "'} WHERE ResultClass=CIM_DataFile")).Get())
                    {
                        if ((from ManagementObject item2 in collection2 select item2["FileName"].ToString()).Any(comp => string.Equals(comp, fileName, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static Object thisLock = new Object();
        public static string FindAndLockATestServer()
        {
            var server = String.Empty;
            lock (thisLock)
            {
                server = FindATestServer();
                LockServer(server);
            }
            return server;
        }

        private static string FindATestServer()
        {
            Log("Finding an available test server.");
            foreach (var server in Settings.RemoteServers)
            {
                var path = $@"\\\\{server}\\C$\\LOCK.txt";
                if (!Impersonate(() => File.Exists(path)))
                {
                    Log($"-- '{server}' is unlocked.");
                    return server;
                }
            }

            var lastModifiedTimes = new Dictionary<String, DateTime>();
            foreach (var server in Settings.RemoteServers)
            {
                var path = $@"\\\\{server}\\C$\\LOCK.txt";
                if (Impersonate(() => File.Exists(path)))
                {
                    var lastWriteTime = Impersonate(() => File.GetLastWriteTime(path));
                    lastModifiedTimes.Add(server, lastWriteTime);
                }
                else
                {
                    return server;
                }
            }
            var bestServer = lastModifiedTimes.OrderByDescending(x => x.Value).Last().Key;
            Log($"-- '{bestServer}' has the shortest wait time.");
            return bestServer;
        }

        public static void LockServer(String server)
        {
            var path = $@"\\\\{server}\\C$\\LOCK.txt";
            Log($"Attempting to lock '{server}'.");
            if (Impersonate(() => File.Exists(path)))
            {
                Log($"-- '{server}' is in use.");
                var timer = Stopwatch.StartNew();

                while (Impersonate(() => File.Exists(path)))
                {
                    try
                    {
                        var lastModified = Impersonate(() => File.GetLastWriteTime(path));
                        if (DateTime.Now > lastModified.AddMinutes(15.0))
                        {
                            Log("-- Lock is over 15 minutes old, proceeding with new lock.");
                            break;
                        }
                        Log($"-- Waiting for lock to expire, elapsed time '{timer.Elapsed}'.");
                        Thread.Sleep(10000);
                    }
                    catch (InvalidOperationException)
                    {
                        Log("-- Caught an exception, lock may have been removed.");
                        break;
                    }

                }
            }
            Impersonate(() => File.Create(path));
            Log($"-- '{server}' locked.");
        }

        public static void Log(String logMessage, string testName = "")
        {
            Console.WriteLine($"{DateTime.Now.ToString("O")} ({testName}) -- {logMessage}");
        }
    }
}
