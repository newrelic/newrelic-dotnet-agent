using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Management;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers.Collections.Generic;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class ProcessExtensions
    {
        public static UInt32 StartRemote([NotNull] String remoteMachineName, [NotNull] String launcherRemoteFilePath, [NotNull] String applicationRemoteFilePath, [NotNull] String newRelicHomeRemoteDirectoryPath, [NotNull] String arguments)
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
            var commandLine = String.Format(@"{0} --application=""{1}"" --newrelichome=""{2}"" --arguments=""{3}""", launcherLocalFilePath, applicationLocalFilePath, newRelicHomeLocalDirectoryPath, arguments.Replace("\"", "`"));
            Console.WriteLine("Command:          {0}", commandLine);
            var processCreateArguments = new Object[] { commandLine, workingDirectory, null, 0 };

            InvokeMethodRemote("Win32_Process", "Create", processCreateArguments, remoteMachineName);
            var processIdObject = processCreateArguments[3];
            Contract.Assert(processIdObject != null);
            return Convert.ToUInt32(processIdObject);
        }

        public static void KillTreeRemote([NotNull] String remoteMachineName, UInt32 processId)
        {
            Contract.Assert(remoteMachineName != null);

            KillProcessChildrenRemote(remoteMachineName, processId);
            KillProcessRemote(remoteMachineName, processId);
        }

        private static void KillProcessRemote([NotNull] String remoteMachineName, UInt32 processId)
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

        public static void KillProcessChildrenRemote([NotNull] String remoteMachineName, UInt32 parentProcessId)
        {
            Contract.Assert(remoteMachineName != null);

            GetChildProcessesRemote(remoteMachineName, parentProcessId)
                .Where(child => child != null)
                .Select(child => Convert.ToUInt32(child["ProcessId"]))
                .ForEachNow(childProcessId => KillTreeRemote(remoteMachineName, childProcessId));
        }

        [NotNull]
        private static IEnumerable<ManagementObject> GetProcessesRemote([NotNull] String remoteMachineName, UInt32 processId)
        {
            var whereClause = String.Format("ProcessId={0}", processId);
            return GetProcessesRemote(remoteMachineName, whereClause);
        }

        [NotNull]
        private static IEnumerable<ManagementObject> GetChildProcessesRemote([NotNull] String remoteMachineName, UInt32 parentProcessId)
        {
            var whereClause = String.Format(@"ParentProcessId={0}", parentProcessId);
            return GetProcessesRemote(remoteMachineName, whereClause);
        }

        [NotNull]
        private static IEnumerable<ManagementObject> GetProcessesRemote([NotNull] String remoteMachineName, [NotNull] String whereClause)
        {
            var wmiScope = String.Format(@"\\{0}\root\cimv2", remoteMachineName);
            var childrenWmiQuery = String.Format(@"SELECT * FROM Win32_Process WHERE {0}", whereClause);
            var children = new ManagementObjectSearcher(wmiScope, childrenWmiQuery).Get();
            return children.Cast<ManagementObject>();
        }

        private static void InvokeMethodRemote([NotNull] String @class, [NotNull] String method, [NotNull] Object[] arguments, [NotNull] String remoteMachineName)
        {
            var connectionOptions = new ConnectionOptions
            {
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.Default,
                EnablePrivileges = true,
            };
            var scopePath = String.Format(@"\\{0}\root\cimv2", remoteMachineName);
            var managementScope = new ManagementScope(scopePath, connectionOptions);
            var managementPath = new ManagementPath(@class);
            new ManagementClass(managementScope, managementPath, null).InvokeMethod(method, arguments);
        }

    }
}
