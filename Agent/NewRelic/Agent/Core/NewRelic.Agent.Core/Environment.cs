using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
#if NET35
using System.Management;
using System.Web.Configuration;
#endif
using System.Web;
using JetBrains.Annotations;
using Microsoft.Win32;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core
{
	[JsonConverter(typeof (EnvironmentConverter))]
	public class Environment
	{
		[NotNull]
		private readonly List<Object[]> _environmentMap = new List<Object[]>();

		public UInt64 TotalPhysicalMemory { get; }
		public String AppDomainAppPath { get; }

		public Environment([NotNull] ISystemInfo systemInfo)
		{
			try
			{
				TotalPhysicalMemory = systemInfo.GetTotalPhysicalMemoryBytes();

				AddVariable("Framework", () => "dotnet");

				var fileVersionInfo = TryGetFileVersionInfo();
				AddVariable("Product Name", () => fileVersionInfo?.ProductName);

				AddVariable("OS", () => System.Environment.OSVersion?.VersionString);
				AddVariable(".NET Version", () => System.Environment.Version.ToString());
				AddVariable("Total Physical System Memory", () => TotalPhysicalMemory);
				AddVariable("x64", () => (IntPtr.Size == 8) ? "yes" : "no");

				var process = TryGetCurrentProcess();
				AddVariable("StartTime", () => process?.StartTime.ToString("o"));
				AddVariable("MainModule.FileVersionInfo", () => process?.MainModule.FileVersionInfo.ToString());

				AddVariable("GCSettings.IsServerGC", () => System.Runtime.GCSettings.IsServerGC);
				AddVariable("AppDomain.FriendlyName", () => AppDomain.CurrentDomain.FriendlyName);

#if NET35
				// This stuff is only available to web apps.
				if (TryGetAppDomainAppId() != null)
				{
					AddVariable("AppDomainAppPath", () => AppDomainAppPath);
					AddVariable("AppDomainAppId", () => HttpRuntime.AppDomainAppId);
					AddVariable("AppDomainAppVirtualPath", () => HttpRuntime.AppDomainAppVirtualPath);
					AppDomainAppPath = TryGetAppPath(() => HttpRuntime.AppDomainAppPath);
					AddVariable("UsingIntegratedPipeline", () => HttpRuntime.UsingIntegratedPipeline.ToString());

					var iisVersion = TryGetIisVersion();
					if(iisVersion != null)
						AddVariable("IIS Version", () => iisVersion.ToString());

					// TODO(rrh): IIS application pool name
				}
#endif

				AddVariable("Plugin List", GetLoadedAssemblyNames);

#if DEBUG
				AddVariable("Debug Build", () => true.ToString());
#endif

#if NET35
				var compilationSection = WebConfigurationManager.GetSection("system.web/compilation") as CompilationSection;
				if (compilationSection?.DefaultLanguage != null)
					AddVariable("system.web.compilation.defaultLanguage", () => compilationSection.DefaultLanguage);

				var managementObjects = TryGetManagementObjects("Select * from Win32_ComputerSystem");
				foreach (var managementObject in managementObjects)
				{
					if (managementObject == null)
						continue;

					AddVariable("Physical Processors", () => managementObject["NumberOfProcessors"]);
					AddVariable("Logical Processors", () => managementObject["NumberOfLogicalProcessors"]);
				}
#endif
			}
			catch (Exception ex)
			{
				Log.Debug($"The .NET agent is unable to collect environment information for the machine: {ex}");
			}
		}

		public void AddVariable([NotNull] String name, [NotNull] Func<Object> valueGetter)
		{
			var value = null as Object;
			try
			{
				value = valueGetter();
			}
			catch (Exception ex)
			{
				Log.Warn($"Error getting value for environment variable {name}: {ex}");
			}

			_environmentMap.Add(new[] {name, value});
		}

		[CanBeNull]
		private static Process TryGetCurrentProcess()
		{
			try
			{
				return Process.GetCurrentProcess();
			}
			catch (Exception ex)
			{
				Log.Warn(ex);
				return null;
			}
		}

		private static FileVersionInfo TryGetFileVersionInfo()
		{
			try
			{
				var assembly = Assembly.GetExecutingAssembly();
				return FileVersionInfo.GetVersionInfo(assembly.Location);
			}
			catch (Exception ex)
			{
				Log.Warn(ex);
				return null;
			}
		}

#if NET35
		[CanBeNull]
		private static String TryGetAppDomainAppId()
		{
			try
			{
				return HttpRuntime.AppDomainAppId;
			}
			catch (Exception ex)
			{
				Log.Warn(ex);
				return null;
			}
		}
#endif

		[CanBeNull]
		public static String TryGetAppPath([NotNull] Func<String> pathGetter)
		{
			try
			{
				var path = pathGetter();

				if (path == null)
					return null;

				if (path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
					path = path.Substring(0, path.Length - 1);

				var index = path.LastIndexOf(System.IO.Path.DirectorySeparatorChar + "wwwroot", StringComparison.InvariantCultureIgnoreCase);
				if (index > 0)
					path = path.Substring(0, index);

				index = path.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
				if (index > 0 && index < path.Length - 1)
					path = path.Substring(index + 1);

				return path;
			}
			catch (Exception ex)
			{
				Log.Warn(ex);
				return null;
			}
		}

#if NET35
		[CanBeNull]
		public Version TryGetIisVersion()
		{
			try
			{
				using (var componentsKey = Registry.LocalMachine?.OpenSubKey(@"Software\Microsoft\InetStp", false))
				{
					if (componentsKey == null)
						return null;

					var majorVersionObject = componentsKey.GetValue("MajorVersion", -1);
					var minorVersionObject = componentsKey.GetValue("MinorVersion", -1);

					if (majorVersionObject == null || minorVersionObject == null)
						return null;

					var majorVersion = (Int32)majorVersionObject;
					var minorVersion = (Int32)minorVersionObject;
					if (majorVersion == -1 || minorVersion == -1)
						return null;

					return new Version(majorVersion, minorVersion);
				}
			}
			catch (Exception ex)
			{
				Log.Warn(ex);
				return null;
			}
		}
#endif

		[NotNull]
		private static IEnumerable<String> GetLoadedAssemblyNames()
		{
			var versionZero = new Version(0, 0, 0, 0);
			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(assembly => assembly != null)
				.Where(assembly => assembly.GetName().Version != versionZero)
#if NET35
				.Where(assembly => !(assembly is System.Reflection.Emit.AssemblyBuilder))
#endif
				.Select(assembly => assembly.FullName)
				.ToList();
		}

#if NET35
		[NotNull]
		private static IEnumerable<ManagementBaseObject> TryGetManagementObjects([NotNull] String query)
		{
			try
			{
				using (var managementObjectSearcher = new ManagementObjectSearcher(query))
				{
					return managementObjectSearcher.Get().Cast<ManagementBaseObject>();
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"Could not retrieve processor count information: {ex}");
				return Enumerable.Empty<ManagementBaseObject>();
			}
		}
#endif

		public class EnvironmentConverter : JsonConverter
		{
			public override void WriteJson([NotNull] JsonWriter writer, Object value, JsonSerializer serializer)
			{
				var environment = value as Environment;
				if (environment == null)
					throw new NullReferenceException("environment");

				var serialized = JsonConvert.SerializeObject(environment._environmentMap);
				writer.WriteRawValue(serialized);
			}

			public override Object ReadJson(JsonReader reader, Type objectType, Object existingValue, JsonSerializer serializer)
			{
				throw new NotImplementedException();
			}

			public override Boolean CanConvert(Type objectType)
			{
				return true;
			}
		}
	}
}
