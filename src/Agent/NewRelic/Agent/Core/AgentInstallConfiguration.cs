// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Core;
using NewRelic.SystemInterfaces;
using Newtonsoft.Json;
using System.Reflection;
using System.Diagnostics;
using NewRelic.Core.Logging;
#if NETFRAMEWORK
using System.Web;
using Microsoft.Win32;
#endif

namespace NewRelic.Agent.Core
{
    public static partial class AgentInstallConfiguration
    {
#if NETSTANDARD2_0
        private const string NewRelicHomeEnvironmentVariable = "CORECLR_NEWRELIC_HOME";
        private const string RuntimeDirectoryName = "netcore";
#else
        private const string NewRelicHomeEnvironmentVariable = "NEWRELIC_HOME";
        private const string RuntimeDirectoryName = "netframework";
#endif
        private const string NewRelicInstallPathEnvironmentVariable = "NEWRELIC_INSTALL_PATH";
        private const string NewRelicLogDirectoryEnvironmentVariable = "NEWRELIC_LOG_DIRECTORY";
        private const string NewRelicLogLevelEnvironmentVariable = "NEWRELIC_LOG_LEVEL";

        public static bool IsWindows { get; }
#if NETFRAMEWORK
		public static DotnetFrameworkVersion DotnetFrameworkVersion { get; }
#else
        public static DotnetCoreVersion DotnetCoreVersion { get; }
#endif
        public static bool IsNetstandardPresent { get; }
        public static bool IsNet46OrAbove { get; }
        public static bool IsNetCore30OrAbove { get; }
        public static string NewRelicHome { get; }
        public static string NewRelicInstallPath { get; }
        public static string NewRelicLogDirectory { get; }
        public static string NewRelicLogLevel { get; }
        public static string HomeExtensionsDirectory { get; }
        public static string RuntimeHomeExtensionsDirectory { get; }
        public static string InstallPathExtensionsDirectory { get; }
        public static int ProcessId { get; }
        public static string AppDomainName { get; }
        public static string AppDomainAppVirtualPath { get; }
        public static AgentInfo AgentInfo { get; }
        public static string AgentVersion { get; }
        public static long AgentVersionTimestamp { get; }

        static AgentInstallConfiguration()
        {
#if NETFRAMEWORK
			IsWindows = true;
#else
            IsWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#endif
            NewRelicHome = GetNewRelicHome();
            NewRelicInstallPath = GetNewRelicInstallPath();
            NewRelicLogDirectory = GetNewRelicLogDirectory();
            NewRelicLogLevel = GetNewRelicLogLevel();
            HomeExtensionsDirectory = NewRelicHome != null ? Path.Combine(NewRelicHome, "extensions") : null;
            RuntimeHomeExtensionsDirectory = HomeExtensionsDirectory != null ? Path.Combine(HomeExtensionsDirectory, RuntimeDirectoryName) : null;
            InstallPathExtensionsDirectory = NewRelicInstallPath != null ? Path.Combine(NewRelicInstallPath, "extensions") : null;
            AgentVersion = GetAgentVersion();
            AgentVersionTimestamp = GetAgentVersionTimestamp();
            IsNetstandardPresent = GetIsNetstandardPresent();
            IsNet46OrAbove = GetIsNet46OrAbove();
            IsNetCore30OrAbove = GetIsNetCore30OrAbove();
            ProcessId = new ProcessStatic().GetCurrentProcess().Id;
            AppDomainName = AppDomain.CurrentDomain.FriendlyName;
#if NETFRAMEWORK
			if (HttpRuntime.AppDomainAppVirtualPath != null)
			{
				AppDomainAppVirtualPath = HttpRuntime.AppDomainAppVirtualPath;
			}

			try
			{
				DotnetFrameworkVersion = DotnetVersion.GetDotnetFrameworkVersion();
			}
			catch { }
#else
            try
            {
                DotnetCoreVersion = DotnetVersion.GetDotnetCoreVersion();
            }
            catch { }
#endif
            AgentInfo = GetAgentInfo();
        }

        //This method and delegate is here to allow dependency injecting the IsWindows logic via a typed delegate
        public delegate bool IsWindowsDelegate();
        public static bool GetIsWindows()
        {
            return IsWindows;
        }

        private static string GetAgentVersion()
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(typeof(AgentInstallConfiguration).Assembly.Location).FileVersion;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to determine agent version.");
                return "?.?.?.?";
            }
        }

        private static long GetAgentVersionTimestamp()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetCustomAttribute<BuildTimestampAttribute>().BuildTimestamp.Value;
            }
            catch
            {
                return 0;
            }
        }

        private static bool GetIsNetstandardPresent()
        {
            var netstandard20Version = new Version(2, 0, 0, 0);
            var netstandardAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == "netstandard");
            return netstandardAssembly != null && netstandardAssembly.GetName().Version >= netstandard20Version;
        }

        private static bool GetIsNet46OrAbove()
        {
#if NETFRAMEWORK
			var net46Version = new Version(4, 0, 30319, 42000);
			return System.Environment.Version >= net46Version;
#else
            return true;
#endif
        }

        private static bool GetIsNetCore30OrAbove()
        {
#if NETFRAMEWORK
			return false;
#else
            var version = System.Environment.Version;

            // Prior to .NET Core 3.0 System.Environment.Version returned 4.0.30319.42000.
            // Therefore, the next major version beyond 3.0 will be 5.0.
            // See: https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#improved-net-core-version-apis
            if (version.Major == 4) return false;

            return version.Major >= 3;
#endif
        }

        private static string GetNewRelicHome()
        {
            var newRelicHome = System.Environment.GetEnvironmentVariable(NewRelicHomeEnvironmentVariable);
            if (newRelicHome != null && Directory.Exists(newRelicHome)) return Path.GetFullPath(newRelicHome);
#if NETFRAMEWORK
			var key = Registry.LocalMachine.OpenSubKey(@"Software\New Relic\.NET Agent");
			if (key != null) newRelicHome = (string)key.GetValue("NewRelicHome");
#endif
            return newRelicHome;
        }

        private static string GetNewRelicInstallPath()
        {
            var newRelicInstallPath = System.Environment.GetEnvironmentVariable(NewRelicInstallPathEnvironmentVariable);
            if (newRelicInstallPath != null)
            {
                newRelicInstallPath = Path.Combine(newRelicInstallPath, RuntimeDirectoryName);
                if (Directory.Exists(newRelicInstallPath)) return newRelicInstallPath;
            }

            newRelicInstallPath = System.Environment.GetEnvironmentVariable(NewRelicHomeEnvironmentVariable);
            return newRelicInstallPath;
        }

        private static string GetNewRelicLogDirectory()
        {
            var newRelicLogDirectory = System.Environment.GetEnvironmentVariable(NewRelicLogDirectoryEnvironmentVariable);
            if (newRelicLogDirectory != null && Directory.Exists(newRelicLogDirectory)) return Path.GetFullPath(newRelicLogDirectory);

            return newRelicLogDirectory;
        }

        private static string GetNewRelicLogLevel()
        {
            return System.Environment.GetEnvironmentVariable(NewRelicLogLevelEnvironmentVariable);
        }

        private static AgentInfo GetAgentInfo()
        {
            var agentInfoPath = $@"{NewRelicHome}\agentinfo.json";
            if (File.Exists(agentInfoPath))
            {
                try
                {
                    return JsonConvert.DeserializeObject<AgentInfo>(File.ReadAllText(agentInfoPath));
                }
                catch
                {
                    // Fail silently
                }
            }

            return null;
        }
    }
}
