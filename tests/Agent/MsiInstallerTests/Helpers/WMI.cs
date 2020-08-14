// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;

namespace FunctionalTests.Helpers
{
    public static class WMI
    {
        /// <summary>
        /// Makes a WMI call.
        /// </summary>
        /// <param name="path">The management path.</param>
        /// <param name="command">The command to be executed.</param>
        public static void MakeWMICall(ManagementScope mgmtScope, String path, String command, String directory = null)
        {
            using (var mgmtClass = new ManagementClass(mgmtScope, new ManagementPath(path), new ObjectGetOptions()))
            {
                var methodParams = mgmtClass.GetMethodParameters("Create");
                methodParams["CommandLine"] = command;
                if (!String.IsNullOrEmpty(directory))
                {
                    methodParams["CurrentDirectory"] = directory;
                }
                var outParams = mgmtClass.InvokeMethod("Create", methodParams, null);
                var pid = Convert.ToInt16(outParams.Properties["ProcessId"].Value);

                // Wait for the process to finish
                Common.Log($"-- Waiting for process {pid} to complete.");
                var maxWait = TimeSpan.FromSeconds(60.0);
                var timer = Stopwatch.StartNew();
                while (timer.Elapsed < maxWait)
                {
                    var proc = new Process();
                    try
                    {
                        proc = Settings.Environment == Enumerations.EnvironmentSetting.Remote
                            ? Common.Impersonate(() => Process.GetProcessById(pid, mgmtScope.Path.Server)) as Process
                            : Process.GetProcessById(pid);
                        Thread.Sleep(1000);
                    }
                    catch (ArgumentException)
                    {
                        Common.Log($"-- Process {pid} has completed in {timer.Elapsed.Seconds} seconds.");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Executes a command against the item(s) returned in a WMI query.
        /// </summary>
        /// <param name="mgmtScope">The management scope.</param>
        /// <param name="query">The query to execute.</param>
        /// <param name="method">The command to execute.</param>
        public static void WMIQuery_InvokeMethod(ManagementScope mgmtScope, String query, String method)
        {
            using (var search = new ManagementObjectSearcher(mgmtScope, new ObjectQuery(query)))
            {
                var mgmtObject = search.Get().Cast<ManagementObject>();
                if (mgmtObject.Any())
                {
                    search.Get().Cast<ManagementObject>().First().InvokeMethod(method, null);
                }
            }
        }

        /// <summary>
        /// Gets the property value on the item returned in a WMI query.
        /// </summary>
        /// <param name="mgmtScope">The management scope.</param>
        /// <param name="query">The query to execute.</param>
        /// <param name="property">The property to get the value of.</param>
        /// <returns></returns>
        public static String WMIQuery_GetPropertyValue(ManagementScope mgmtScope, String query, String property)
        {
            using (var search = new ManagementObjectSearcher(mgmtScope, new ObjectQuery(query)))
            {
                return search.Get().Cast<ManagementObject>().First().GetPropertyValue(property).ToString();
            }
        }
    }
}
