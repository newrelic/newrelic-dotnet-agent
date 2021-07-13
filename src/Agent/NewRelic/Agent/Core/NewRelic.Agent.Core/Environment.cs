// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
#if NET45
using System.Management;
using System.Web.Configuration;
#endif
using System.Web;
using Microsoft.Win32;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using Newtonsoft.Json;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core
{
    [JsonConverter(typeof(EnvironmentConverter))]
    public class Environment
    {
        private readonly List<object[]> _environmentMap = new List<object[]>();

        private readonly IProcessStatic _processStatic;

        public ulong? TotalPhysicalMemory { get; }
        public string AppDomainAppPath { get; }

        public Environment(ISystemInfo systemInfo, IProcessStatic processStatic, IConfigurationService configurationService)
        {
            _processStatic = processStatic;

            try
            {
                TotalPhysicalMemory = systemInfo.GetTotalPhysicalMemoryBytes();

                AddVariable("Framework", () => "dotnet");

                var fileVersionInfo = TryGetFileVersionInfo();
                AddVariable("Product Name", () => fileVersionInfo?.ProductName);

                AddVariable("OS", () => System.Environment.OSVersion?.VersionString);
#if NETSTANDARD2_0
				// This API is only supported on .net FX 4.7 + so limiting it
				// to .net core since that is the one affected. 
				AddVariable(".NET Version", () => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.ToString());
#else
                AddVariable(".NET Version", () => System.Environment.Version.ToString());
#endif
                AddVariable("Total Physical System Memory", () => TotalPhysicalMemory);
                AddVariable("x64", () => (IntPtr.Size == 8) ? "yes" : "no");

                var process = TryGetCurrentProcess();
                AddVariable("StartTime", () => process?.StartTime.ToString("o"));
                AddVariable("MainModule.FileVersionInfo", () => process?.FileVersionInfo.ToString());

                AddVariable("GCSettings.IsServerGC", () => System.Runtime.GCSettings.IsServerGC);
                AddVariable("AppDomain.FriendlyName", () => AppDomain.CurrentDomain.FriendlyName);

                if (configurationService?.Configuration?.ApplicationNames?.Any() ?? false)
                    AddVariable("Application Name Source", () => configurationService.Configuration.ApplicationNamesSource);

#if NET45
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

				}
#endif

                AddVariable("Plugin List", GetLoadedAssemblyNames);

#if DEBUG
				AddVariable("Debug Build", () => true.ToString());
#endif

#if NET45
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

        public void AddVariable(string name, Func<object> valueGetter)
        {
            var value = null as object;
            try
            {
                value = valueGetter();
            }
            catch (Exception ex)
            {
                Log.Warn($"Error getting value for environment variable {name}: {ex}");
            }

            _environmentMap.Add(new[] { name, value });
        }

        private IProcess TryGetCurrentProcess()
        {
            try
            {
                return _processStatic.GetCurrentProcess();
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

#if NET45
		private static string TryGetAppDomainAppId()
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

        public static string TryGetAppPath(Func<string> pathGetter)
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

#if NET45
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

					var majorVersion = (int)majorVersionObject;
					var minorVersion = (int)minorVersionObject;
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

        private static IEnumerable<string> GetLoadedAssemblyNames()
        {
            var versionZero = new Version(0, 0, 0, 0);
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly != null)
                .Where(assembly => assembly.GetName().Version != versionZero)
#if NET45
				.Where(assembly => !(assembly is System.Reflection.Emit.AssemblyBuilder))
#endif
                .Select(assembly => assembly.FullName)
                .ToList();
        }

#if NET45
		private static IEnumerable<ManagementBaseObject> TryGetManagementObjects(string query)
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
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var environment = value as Environment;
                if (environment == null)
                    throw new NullReferenceException("environment");

                var serialized = JsonConvert.SerializeObject(environment._environmentMap);
                writer.WriteRawValue(serialized);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override bool CanConvert(Type objectType)
            {
                return true;
            }
        }
    }
}
