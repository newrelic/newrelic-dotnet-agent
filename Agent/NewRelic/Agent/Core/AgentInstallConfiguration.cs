using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
#if NET45
using System.Web;
using Microsoft.Win32;
#endif

namespace NewRelic.Agent.Core
{
	public static class AgentInstallConfiguration
	{
#if NETSTANDARD2_0
		private const string NewRelicHomeEnvironmentVariable = "CORECLR_NEWRELIC_HOME";
		private const string NewRelicInstallPathEnvironmentVariable = "CORECLR_NEWRELIC_INSTALL_PATH";
#else
		private const string NewRelicHomeEnvironmentVariable = "NEWRELIC_HOME";
		private const string NewRelicInstallPathEnvironmentVariable = "NEWRELIC_INSTALL_PATH";
#endif

		public static bool IsWindows { get; }
		public static bool IsNetstandardPresent { get; }
		public static bool IsNet46OrAbovePresent { get; }
		public static string NewRelicHome { get; }
		public static string NewRelicInstallPath { get; }
		public static string HomeExtensionsDirectory { get; }
		public static string InstallPathExtensionsDirectory { get; }
		public static int ProcessId { get; }
		public static string AppDomainName { get; }
		public static string AppDomainAppVirtualPath { get; }

		static AgentInstallConfiguration()
		{
#if NET45
			IsWindows = true;
#else
			IsWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#endif
			NewRelicHome = GetNewRelicHome();
			NewRelicInstallPath = GetNewRelicInstallPath();
			HomeExtensionsDirectory = NewRelicHome != null ? Path.Combine(NewRelicHome, "extensions") : null;
			InstallPathExtensionsDirectory = NewRelicInstallPath != null ? Path.Combine(NewRelicInstallPath, "extensions") : null;
			IsNetstandardPresent = GetIsNetstandardPresent();
			IsNet46OrAbovePresent = GetIsNet46OrAbovePresent();
			ProcessId = Process.GetCurrentProcess().Id;
			AppDomainName = AppDomain.CurrentDomain.FriendlyName;
#if NET45
			if (HttpRuntime.AppDomainAppVirtualPath != null)
			{
				AppDomainAppVirtualPath = HttpRuntime.AppDomainAppVirtualPath;
			}
#endif
		}

		private static bool GetIsNetstandardPresent()
		{
			var netstandard20Version = new Version(2, 0, 0, 0);
			var netstandardAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == "netstandard");
			return netstandardAssembly != null && netstandardAssembly.GetName().Version >= netstandard20Version;
		}
		private static bool GetIsNet46OrAbovePresent()
		{
#if NET45
			var net46Version = new Version(4, 0, 30319, 42000);
			return System.Environment.Version >= net46Version;
#else
			return true;
#endif
		}

		private static string GetNewRelicHome()
		{
			var newRelicHome = System.Environment.GetEnvironmentVariable(NewRelicHomeEnvironmentVariable);
			if (newRelicHome != null && Directory.Exists(newRelicHome)) return newRelicHome;
#if NET45
			var key = Registry.LocalMachine.OpenSubKey(@"Software\New Relic\.NET Agent");
			if (key != null) newRelicHome = (string)key.GetValue("NewRelicHome");
#endif
			return newRelicHome;
		}

		private static string GetNewRelicInstallPath()
		{
			var newRelicInstallPath = System.Environment.GetEnvironmentVariable(NewRelicInstallPathEnvironmentVariable);
			if (newRelicInstallPath != null && Directory.Exists(newRelicInstallPath)) return newRelicInstallPath;
			newRelicInstallPath = System.Environment.GetEnvironmentVariable(NewRelicHomeEnvironmentVariable);
			return newRelicInstallPath;
		}
	}
}
