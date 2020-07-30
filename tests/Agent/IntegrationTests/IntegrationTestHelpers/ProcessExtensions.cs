/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Management;
using NewRelic.Agent.IntegrationTestHelpers.Collections.Generic;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class ProcessExtensions
    {
        public static uint StartRemote(string remoteMachineName, string launcherRemoteFilePath, string applicationRemoteFilePath, string newRelicHomeRemoteDirectoryPath, string arguments)
        {
            Contract.Assert(remoteMachineName != null);
            Contract.Assert(launcherRemoteFilePath != null);
            Contract.Assert(applicationRemoteFilePath != null);
            Contract.Assert(newRelicHomeRemoteDirectoryPath != null);
            Contract.Assert(arguments != null);

            var launcherLocalFilePath = CommonUtils.GetLocalPathFromRemotePath(launcherRemoteFilePath);
            var applicationLocalFilePath = CommonUtils.GetLocalPathFromRemotePath(applicationRemoteFilePath);
            var newRelicHomeLocalDirectoryPath = CommonUtils.GetLocalPathFromRemotePath(newRelicHomeRemoteDirectoryPath);
            var workingDirectory = Path.GetDirectoryName(launcherLocalFilePath);
            var commandLine = string.Format(@"{0} --application=""{1}"" --newrelichome=""{2}"" --arguments=""{3}""", launcherLocalFilePath, applicationLocalFilePath, newRelicHomeLocalDirectoryPath, arguments.Replace("\"", "`"));
            Console.WriteLine("Command:          {0}", commandLine);
            var processCreateArguments = new object[] { commandLine, workingDirectory, null, 0 };

            InvokeMethodRemote("Win32_Process", "Create", processCreateArguments, remoteMachineName);
            var processIdObject = processCreateArguments[3];
            Contract.Assert(processIdObject != null);
            return Convert.ToUInt32(processIdObject);
        }

        public static void KillTreeRemote(string remoteMachineName, uint processId)
        {
            Contract.Assert(remoteMachineName != null);

            KillProcessChildrenRemote(remoteMachineName, processId);
            KillProcessRemote(remoteMachineName, processId);
        }

        private static void KillProcessRemote(string remoteMachineName, uint processId)
        {
            Contract.Assert(remoteMachineName != null);

            try
            {
                GetProcessesRemote(remoteMachineName, processId)
                    .Where(process => process != null)
                    .ForEachNow(process => process.InvokeMethod("Terminate", null));
            }
            catch (Exception exception)
            {
                Console.WriteLine("Exception thrown while attempting to remotely kill process (PID: {0}).", processId);
                Console.WriteLine(exception);
            }
        }

        public static void KillProcessChildrenRemote(string remoteMachineName, uint parentProcessId)
        {
            Contract.Assert(remoteMachineName != null);

            GetChildProcessesRemote(remoteMachineName, parentProcessId)
                .Where(child => child != null)
                .Select(child => Convert.ToUInt32(child["ProcessId"]))
                .ForEachNow(childProcessId => KillTreeRemote(remoteMachineName, childProcessId));
        }

        private static IEnumerable<ManagementObject> GetProcessesRemote(string remoteMachineName, uint processId)
        {
            var whereClause = string.Format("ProcessId={0}", processId);
            return GetProcessesRemote(remoteMachineName, whereClause);
        }

        private static IEnumerable<ManagementObject> GetChildProcessesRemote(string remoteMachineName, uint parentProcessId)
        {
            var whereClause = string.Format(@"ParentProcessId={0}", parentProcessId);
            return GetProcessesRemote(remoteMachineName, whereClause);
        }

        private static IEnumerable<ManagementObject> GetProcessesRemote(string remoteMachineName, string whereClause)
        {
            var wmiScope = string.Format(@"\\{0}\root\cimv2", remoteMachineName);
            var childrenWmiQuery = string.Format(@"SELECT * FROM Win32_Process WHERE {0}", whereClause);
            var children = new ManagementObjectSearcher(wmiScope, childrenWmiQuery).Get();
            return children.Cast<ManagementObject>();
        }

        private static void InvokeMethodRemote(string @class, string method, object[] arguments, string remoteMachineName)
        {
            var connectionOptions = new ConnectionOptions
            {
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.Default,
                EnablePrivileges = true,
            };
            var scopePath = string.Format(@"\\{0}\root\cimv2", remoteMachineName);
            var managementScope = new ManagementScope(scopePath, connectionOptions);
            var managementPath = new ManagementPath(@class);
            new ManagementClass(managementScope, managementPath, null).InvokeMethod(method, arguments);
        }

    }
}
