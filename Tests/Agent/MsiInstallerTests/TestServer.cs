using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Xml;
using System.Xml.XPath;
using FunctionalTests.Helpers;
using Microsoft.Win32;
using NUnit.Framework;

namespace FunctionalTests
{
	public class TestServer
	{
		private String _address;
		public String Address { get { return _address; } }

		private String _configPath;
		public String ConfigPath { get { return _configPath; } }

		private String _dataPath;
		public String DataPath { get { return _dataPath; } set { _dataPath = value; } }

		private String _installPath;
		public String InstallPath { get { return _installPath; } }

		private String _driveRoot;
		public String DriveRoot { get { return _driveRoot; } }

		private ConnectionOptions _connection;
		public ConnectionOptions Connection { get { return _connection; } }

		private ManagementScope _mgmtScope;
		public ManagementScope MgmtScope { get { return _mgmtScope; } }

		private String _processorArchitecture;
		public String ProcessorArchitecture { get { return _processorArchitecture; } }

		private static String _processorBit;
		public static String ProcessorBit { get { return _processorBit; } }

		public TestServer(String address = null, bool factoryReset = false)
		{
			if (address != null)
			{
				_address = address;
				Common.LockServer(_address);
			}
			else
			{
				if (Settings.RemoteServers.Length > 1 && Settings.Environment == Enumerations.EnvironmentSetting.Remote)
				{
					_address = Common.FindAndLockATestServer();
				}
				else if (Settings.RemoteServers.Length == 1 && Settings.Environment == Enumerations.EnvironmentSetting.Remote)
				{
					_address = Settings.RemoteServers[0];
					Common.LockServer(_address);
				}
				else
				{
					_address = System.Environment.MachineName;
				}
			}
			
			if (Settings.Environment == Enumerations.EnvironmentSetting.Remote)
			{
				_connection = new ConnectionOptions { Authentication = AuthenticationLevel.PacketPrivacy, Username = "Administrator", Password = "!4maline!" };
				_driveRoot = $@"\\{_address}\C$\";
			}
			else
			{
				_connection = new ConnectionOptions { Authentication = AuthenticationLevel.PacketPrivacy };
				_driveRoot = @"C:\";
			}

			_dataPath = $@"{_driveRoot}ProgramData\New Relic\.NET Agent\";
			_installPath = $@"{_driveRoot}Program Files\New Relic\.NET Agent\";

			_mgmtScope = new ManagementScope { Options = _connection, Path = new ManagementPath($@"\\{_address}\root\cimv2") };

			_configPath = $@"{_dataPath}\newrelic.config";
			_processorArchitecture = WMI.WMIQuery_GetPropertyValue(_mgmtScope, "SELECT * FROM Win32_OperatingSystem", "OSArchitecture") == "64-bit"
				? "x64"
				: "x86";
			_processorBit = _processorArchitecture == "x64"
				? "64"
				: "32";

			if (factoryReset)
			{
				FactoryReset();
			}
		}

		/// <summary>
		/// Performs a 'Factory Reset' on the test server.
		/// </summary>
		public void FactoryReset()
		{
			Common.Log($"Factory reset executing on '{_address}'");
			IISCommand("Stop");

			DeleteEnvironmentVariable("COR_ENABLE_PROFILING");

			var configPath = @"C:\\ProgramData\\New Relic\\.NET Agent\\newrelic.config";
			if (FileOperations.FileOrDirectoryExists(_mgmtScope, configPath))
			{
				FileOperations.DeleteFileOrDirectory(_mgmtScope, configPath);
			}

			var source = $@"{TestContext.CurrentContext.TestDirectory}\\..\\..\\default_newrelic.config";
			var destination = $@"{_dataPath}\\newrelic.config";
			FileOperations.CopyFile(source, destination);

			PurgeAgentLogs();

			ModifyOrCreateXmlAttribute("//x:service", "host", "staging-collector.newrelic.com", _configPath);
			ModifyOrCreateXmlAttribute("//x:service", "licenseKey", Settings.LicenseKey, _configPath);
			ModifyOrCreateXmlAttribute("//x:configuration/x:log", "level", "debug", _configPath);
			ModifyOrCreateXmlAttribute("//x:configuration/x:log", "auditLog", "true", _configPath);

			IISCommand("Start");

			EventLog_Clear("Application");
		}

		/// <summary>
		/// Purges the contents of 'C:\ProgramData\New Relic\.NET Agent\Logs'
		/// </summary>
		public void PurgeAgentLogs()
		{
			Common.Log($"Purging Agent logs on '{_address}'");
			FileOperations.DeleteFileOrDirectory(_mgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Logs", true);
		}

		/// <summary>
		/// Executes the specified command against IIS.
		/// </summary>
		/// <param name="command">The command to execute ('Stop', 'Start', or 'Reset').</param>
		public void IISCommand(String command)
		{
			Common.Log($"Executing IIS '{command}' on '{_address}'");
			var iisCommand = command == "Reset"
				? String.Empty
				: $" /{command}";
			WMI.MakeWMICall(_mgmtScope, "Win32_Process", $@"cmd.exe /c iisreset.exe{iisCommand}");
		}

		public void UpdateServerLock()
		{
			Common.Log(String.Format($"Refreshing lock on '{_address}'."));
			FileOperations.CreateFileOrDirectory(_mgmtScope, @"C:\LOCK.txt");
		}

		public void ReleaseServerLock()
		{
			Common.Log(String.Format($"Releasing lock on '{_address}'"));
			FileOperations.DeleteFileOrDirectory(_mgmtScope, @"C:\\LOCK.txt");
		}

		#region Event Log
		/// <summary>
		/// Clears the specified event log.
		/// </summary>
		/// <param name="eventLogName">The name of the event log.</param>
		public void EventLog_Clear(String eventLogName)
		{
			var query = $"SELECT * FROM Win32_NTEventLogFile WHERE LogFileName = '{eventLogName}'";
			WMI.WMIQuery_InvokeMethod(_mgmtScope, query, "ClearEventLog");
		}

		/// <summary>
		/// Checks the specified event log for errors.
		/// </summary>
		/// <param name="eventLogName">The name of the event log.</param>
		/// <returns>A List of the error messages.</returns>
		public List<string> EventLog_CheckForErrors(String eventLogName)
		{
			var errors = new List<String>();
			var query = $"SELECT * FROM Win32_NTLogEvent WHERE Logfile = '{eventLogName}' AND Type = 'Error'";
			using (var collection = new ManagementObjectSearcher(_mgmtScope, new ObjectQuery(query)).Get())
			{
				errors.AddRange(from ManagementObject item in collection select item["Message"].ToString());
			}

			// We sometimes get WMI errors for WebAdministration. These errors are irrelevant to us.
			errors = errors
				.Where(error => error != "Access to the root\\WebAdministration namespace was denied because the namespace is marked with RequiresEncryption but the script or application attempted to connect to this namespace with an authentication level below Pkt_Privacy. Change the authentication level to Pkt_Privacy and run the script or application again.")
				.ToList();

			return errors;
		}
		#endregion Event Log

		#region Environment Variables
		/// <summary>
		/// Checks if the specified environment variable exists.
		/// </summary>
		/// <param name="environmentVariable">The name of the environment variable.</param>
		/// <returns>Boolean indicating if the variable was or was not found.</returns>
		public bool EnvironmentVariableExists(String environmentVariable)
		{
			var query = new SelectQuery("Win32_Environment", $"Name='{environmentVariable}'");
			using (var searcher = new ManagementObjectSearcher(_mgmtScope, query))
			{
				return searcher.Get().Cast<ManagementObject>().Any();
			}
		}

		/// <summary>
		/// Creates an environment variable.
		/// </summary>
		/// <param name="name">The name of the environment variable.</param>
		/// <param name="value">The value of the environment variable.</param>
		public void CreateEnvironmentVariable(string name, string value)
		{
			if (EnvironmentVariableExists(name)) return;

			using (var mgmtClass = new ManagementClass(_mgmtScope, new ManagementPath("Win32_Environment"), new ObjectGetOptions()))
			{
				var mo = mgmtClass.CreateInstance();

				if (mo == null) return;
				mo["Name"] = name;
				mo["UserName"] = "<System>";
				mo["VariableValue"] = value;
				mo.Put();
			}
		}

		/// <summary>
		/// Deletes an environment variable.
		/// </summary>
		/// <param name="environmentVariable">The name of the environment variable.</param>
		/// <returns>The value of the environment variable.</returns>
		public void DeleteEnvironmentVariable(string environmentVariable)
		{
			var query = new SelectQuery("Win32_Environment", $"Name='{environmentVariable}'");
			using (var searcher = new ManagementObjectSearcher(_mgmtScope, query))
			{
				foreach (ManagementObject envVar in searcher.Get())
				{
					envVar.Delete();
				}
			}
		}

		/// <summary>
		/// Fetches the value of the specified environment variable.
		/// </summary>
		/// <param name="environmentVariable">The name of the environment variable.</param>
		/// <returns>The value of the environment variable.</returns>
		public string FetchEnvironmentVariableValue(String environmentVariable)
		{
			var query = $"SELECT * FROM Win32_Environment WHERE Name='{environmentVariable}'";
			var value = WMI.WMIQuery_GetPropertyValue(_mgmtScope, query, "variableValue");

			return value;
		}
		#endregion Environment Variables

		#region Registry
		/// <summary>
		/// Creates or updates the specified registry key, creating or updating the specified name/value.
		/// </summary>
		/// <param name="hive"></param>
		/// <param name="path"></param>
		/// <param name="name"></param>
		/// <param name="value"></param>
		public void CreateOrUpdateRegistryKey(RegistryHive hive, string path, string name, object value)
		{
			var view = path.Contains(@"Wow6432Node\CLSID\")
				? RegistryView.Registry32
				: RegistryView.Registry64;
			path = path.Replace(@"Wow6432Node\CLSID\", String.Empty);

			var registryKey = Settings.Environment == Enumerations.EnvironmentSetting.Remote
				? RegistryKey.OpenRemoteBaseKey(hive, _address) as RegistryKey
				: RegistryKey.OpenBaseKey(hive, view);
			var subKey = registryKey.OpenSubKey(path, true) ?? registryKey.CreateSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree);
			subKey.SetValue(name, value);
		}

		/// <summary>
		/// Fetches the value of the specified registry key.
		/// </summary>
		/// <param name="hive">The registry hive.</param>
		/// <param name="sKey">The path to the key.</param>
		/// <param name="key">The key to fetch the value for.</param>
		public object GetRegistryKeyValue(RegistryHive hive, String sKey, String key)
		{
			object keyValue;
			var view = sKey.Contains(@"Wow6432Node\CLSID\")
				? RegistryView.Registry32
				: RegistryView.Registry64;
			sKey = sKey.Replace(@"Wow6432Node\CLSID\", String.Empty);

			var registryKey = Settings.Environment == Enumerations.EnvironmentSetting.Remote
				? Common.Impersonate(() => RegistryKey.OpenRemoteBaseKey(hive, _address, view)) as RegistryKey
				: RegistryKey.OpenBaseKey(hive, view);
			var subKey = registryKey.OpenSubKey(sKey);
			keyValue = subKey.GetValue(key);

			return keyValue;
		}

		/// <summary>
		/// Deletes a registry key.
		/// </summary>
		/// <param name="hive">The registry hive.</param>
		/// <param name="sKey">The registry sub key.</param>
		/// <param name="key">The registry key.</param>
		public void DeleteRegistryKey(RegistryHive hive, string sKey, string key)
		{
			var view = sKey.Contains(@"Wow6432Node\CLSID\")
				? RegistryView.Registry32
				: RegistryView.Registry64;
			sKey = sKey.Replace(@"Wow6432Node\CLSID\", String.Empty);

			var registryKey = Settings.Environment == Enumerations.EnvironmentSetting.Remote
				? Common.Impersonate(() => RegistryKey.OpenRemoteBaseKey(hive, _address, view)) as RegistryKey
				: RegistryKey.OpenBaseKey(hive, view);
			var subKey = registryKey.OpenSubKey(sKey, true);

			try
			{
				subKey.DeleteValue(key);
			}
			catch (Exception) { }
		}

		public void DeleteRegistryValue(RegistryHive hive, string path, string name)
		{
			var view = path.Contains(@"Wow6432Node\CLSID\")
				? RegistryView.Registry32
				: RegistryView.Registry64;
			path = path.Replace(@"Wow6432Node\CLSID\", String.Empty);

			var registryKey = Settings.Environment == Enumerations.EnvironmentSetting.Remote
				? Common.Impersonate(() => RegistryKey.OpenRemoteBaseKey(hive, _address, view)) as RegistryKey
				: RegistryKey.OpenBaseKey(hive, view);
			var subKey = registryKey.OpenSubKey(path, true);
			subKey.DeleteValue(name);
		}

		public bool RegistryKeyValueExists(RegistryHive hive, string path, string name)
		{
			object keyValue;
			var view = path.Contains(@"Wow6432Node\CLSID\")
				? RegistryView.Registry32
				: RegistryView.Registry64;
			path = path.Replace(@"Wow6432Node\CLSID\", String.Empty);

			var registryKey = Settings.Environment == Enumerations.EnvironmentSetting.Remote
				? Common.Impersonate(() => RegistryKey.OpenRemoteBaseKey(hive, _address, view)) as RegistryKey
				: RegistryKey.OpenBaseKey(hive, view);
			var subKey = registryKey.OpenSubKey(path);
			keyValue = subKey.GetValue(name, "Not Found");

			return (string)keyValue == "Not Found" ? false : true;
		}

		public bool RegistryKeyExists(RegistryHive hive, string path)
		{
			var view = path.Contains(@"Wow6432Node\CLSID\")
				? RegistryView.Registry32
				: RegistryView.Registry64;
			path = path.Replace(@"Wow6432Node\CLSID\", String.Empty);

			var registryKey = Settings.Environment == Enumerations.EnvironmentSetting.Remote
				? Common.Impersonate(() => RegistryKey.OpenRemoteBaseKey(hive, _address, view)) as RegistryKey
				: RegistryKey.OpenBaseKey(hive, view);
			var subKey = registryKey.OpenSubKey(path);
			return subKey != null;
		}

		public string[] GetRegistryKeySubKeys(RegistryHive hive, string path)
		{
			var view = path.Contains(@"Wow6432Node\CLSID\")
				? RegistryView.Registry32
				: RegistryView.Registry64;
			path = path.Replace(@"Wow6432Node\CLSID\", String.Empty);

			var registryKey = Settings.Environment == Enumerations.EnvironmentSetting.Remote
				? Common.Impersonate(() => RegistryKey.OpenRemoteBaseKey(hive, _address, view)) as RegistryKey
				: RegistryKey.OpenBaseKey(hive, view);
			var subKey = registryKey.OpenSubKey(path);
			return subKey.GetSubKeyNames();
		}

		public List<string> GetRegistryKeyValuePairs(RegistryHive hive, string path)
		{
			var view = path.Contains(@"Wow6432Node\CLSID\")
				? RegistryView.Registry32
				: RegistryView.Registry64;
			path = path.Replace(@"Wow6432Node\CLSID\", String.Empty);

			var registryKey = Settings.Environment == Enumerations.EnvironmentSetting.Remote
				? Common.Impersonate(() => RegistryKey.OpenRemoteBaseKey(hive, _address, view)) as RegistryKey
				: RegistryKey.OpenBaseKey(hive, view);
			var subKey = registryKey.OpenSubKey(path);

			var pairs = new List<string>();

			if (subKey == null || subKey.ValueCount == 0)
			{
				return pairs;
			}

			var names = subKey.GetValueNames();
			foreach (var name in names)
			{
				pairs.Add(name + "=" + subKey.GetValue(name, "NOT FOUND"));
			}

			return pairs;
		}

		#endregion Registry

		#region Install/Uninstall
		/// <summary>
		/// Installs the .NET Agent via a command line call to msiexec.exe.
		/// </summary>
		/// <param name="targetDir">The target directory for the install.</param>
		public void CommandLineInstallOldInstall()
		{
			IISCommand("Stop");
			
			FileOperations.DeleteFileOrDirectory(_mgmtScope, @"C:\\installLogOld.txt");

			var command =
				$@"msiexec.exe /i C:\NewRelicAgent_{_processorArchitecture}_2.8.1.0.msi /norestart /quiet NR_LICENSE_KEY={Settings.LicenseKey} /lv* C:\installLogOld.txt";
			Common.Log($"MSIEXEC command: {command}");
			WMI.MakeWMICall(_mgmtScope, "Win32_Process", command);

			var configPath = String.Empty;
			if (Settings.Environment == Enumerations.EnvironmentSetting.Local)
			{
				configPath = @"C:\ProgramData\New Relic\.NET Agent\newrelic.xml";
			}
			else
			{
				configPath = $@"\\{_address}\C$\ProgramData\New Relic\.NET Agent\newrelic.xml";
			}

			// Set the host attribute value to staging, audit logging to 'true'
			ModifyOrCreateXmlAttribute("//x:service", "host", "staging-collector.newrelic.com", configPath);
			ModifyOrCreateXmlAttribute("//x:configuration/x:log", "auditLog", "true", configPath);

			// Create the subfolders
			FileOperations.CreateFileOrDirectory(_mgmtScope, $@"C:\ProgramData\New Relic\.NET Agent\extensions\ExtensionsSubdirectory", true);

			// Create the files
			FileOperations.CreateFileOrDirectory(_mgmtScope, $@"C:\ProgramData\New Relic\.NET Agent\logs\Log.txt");
			FileOperations.CreateFileOrDirectory(_mgmtScope, $@"C:\ProgramData\New Relic\.NET Agent\extensions\Extensions.txt");
			FileOperations.CreateFileOrDirectory(_mgmtScope, $@"C:\ProgramData\New Relic\.NET Agent\extensions\ExtensionsSubdirectory\ExtensionsSubdirectory.txt");

			IISCommand("Start");
		}

		/// <summary>
		/// Uninstalls the .NET Agent via a WMI call.
		/// </summary>
		/// <param name="purge">Flag used to purge remaining file(s) after uninstall.</param>
		public void CommandLineUninstall(bool purge = false, string testName = "")
		{
			Common.Log(String.Format("Uninstalling the .NET Agent"), testName);
			IISCommand("Stop");

			// Uninstall the agent
			FileOperations.DeleteFileOrDirectory(_mgmtScope, @"C:\\uninstallLog.txt");

			if (File.Exists($@"{Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi"))
			{
				Common.Log($@"Found {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi", testName);
			}
			else
			{
				Common.Log($@"ERROR: Did not find: {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi", testName);
			}

			var command =
				$@"msiexec.exe /x {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi /norestart /quiet /lv* C:\uninstallLog.txt";
			Common.Log($"MSIEXEC command: {command}", testName);
			WMI.MakeWMICall(_mgmtScope, "Win32_Process", command);

			// Purge files if specified
			if (purge)
			{
				// Set the file paths
				var paths = new[]
				{
					@"C:\\Program Files\\New Relic",
					@"C:\\ProgramData\\New Relic"
				};

				// Recursively deleted the specified paths
				foreach (var path in paths)
				{
					FileOperations.DeleteFileOrDirectory(_mgmtScope, path, true);
				}

				// Delete the agent environment variables
				DeleteEnvironmentVariable("COR_ENABLE_PROFILING");
				DeleteEnvironmentVariable("COR_PROFILER");
				DeleteEnvironmentVariable("COR_PROFILER_PATH");
				DeleteEnvironmentVariable("NEWRELIC_HOME");
				DeleteEnvironmentVariable("NEW_RELIC_HOME");
				DeleteEnvironmentVariable("NEW_RELIC_HOST");
				DeleteEnvironmentVariable("NEWRELIC_LICENSEKEY");

				// Delete the WAS and W3SVC Environment
				DeleteRegistryKey(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\W3SVC", "Environment");
				DeleteRegistryKey(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WAS", "Environment");
			}

			IISCommand("Start");
		}

		/// <summary>
		/// Performs a repair on the current installed version of the .NET agent via the command line.
		/// </summary>
		public void CommandLineRepair(string testName = "")
		{
			Common.Log(String.Format("Repairing the .NET Agent"), testName);
			IISCommand("Stop");

			// Make a call to perform the repair
			FileOperations.DeleteFileOrDirectory(_mgmtScope, @"C:\\repairLog.txt");

			if (File.Exists($@"{Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi"))
			{
				Common.Log($@"Found {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi", testName);
			}
			else
			{
				Common.Log($@"ERROR: Did not find: {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi", testName);
			}

			var command =
				$@"msiexec.exe /f {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi /quiet /lv* C:\repairLog.txt";
			Common.Log($"MSIEXEC command: {command}", testName);
			WMI.MakeWMICall(_mgmtScope, "Win32_Process", command);

			IISCommand("Start");
		}

		/// <summary>
		/// Installs the .NET Agent via a command line call to msiexec.exe.
		/// </summary>
		/// <param name="licenseKey">The account license key.</param>
		/// <param name="features">The optional list of features to install.</param>
		/// <param name="allFeatures">Option to install all options - this will override any/all features specified in the 'features' parameter.</param>
		public void CommandLineInstall(String licenseKey, List<Enumerations.InstallFeatures> features = null, bool allFeatures = false, bool setCollectorHost = true, string testName = "")
		{
			Common.Log(String.Format("Installing the .NET Agent"), testName);
			IISCommand("Stop");

			// Make a wmi call to perform the install
			var addKey = !String.IsNullOrEmpty(licenseKey)
				? $" NR_LICENSE_KEY={licenseKey}"
				: String.Empty;
			var featuresList = String.Empty;
			var command = String.Empty;
			FileOperations.DeleteFileOrDirectory(_mgmtScope, @"C:\\installLog.txt");

			if (File.Exists($@"{Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi"))
			{
				Common.Log($@"Found {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi", testName);
			}
			else
			{
				Common.Log($@"ERROR: Did not find: {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi", testName);
			}

			if (allFeatures)
			{
				command =
					$@"msiexec.exe /i {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi /norestart /quiet{addKey} INSTALLLEVEL=50 /lv* C:\installLog.txt";
			}
			else
			{
				if (features != null)
				{
					featuresList = features.Aggregate(" ADDLOCAL=", (current, feature) => current + (feature.ToString() + ","));
					featuresList = featuresList.TrimEnd(',');
				}

				command =
					$@"msiexec.exe /i {Settings.Workspace}\Build\BuildArtifacts\MsiInstaller-x64\NewRelicAgent_{_processorArchitecture}_{Settings.AgentVersion}.msi /norestart /quiet{addKey}{featuresList} /lv* C:\installLog.txt";
			}

			Common.Log($"MSIEXEC command: {command}", testName);
			WMI.MakeWMICall(_mgmtScope, "Win32_Process", command);

			if (setCollectorHost)
				ModifyOrCreateXmlAttribute("//x:service", "host", "staging-collector.newrelic.com");

			ModifyOrCreateXmlAttribute("//x:configuration/x:log", "level", "debug");
			ModifyOrCreateXmlAttribute("//x:configuration/x:log", "auditLog", "true");
			IISCommand("Start");
		}

		/// <summary>
		/// Completely removes the .Net Agent to prepare for next test. Calls CommandLineUninstall(true) first and then double checks.
		/// </summary>
		public void RunCleanUninstall(bool attemptUninstall = true, string testName = "")
		{
			if (attemptUninstall)
			{
				CommandLineUninstall(true, testName: testName);
			}

			CleanProgramFiles();
			CleanProgramData();
			CleanEnvironmentVariables();
			CleanRegistry();
			CleanProgramFilesx86();
		}

		private void CleanProgramFiles()
		{
			var path = @"C:\\Program Files\\New Relic\\.NET Agent";
			if (FileOperations.FileOrDirectoryExists(MgmtScope, path))
			{
				FileOperations.DeleteFileOrDirectory(MgmtScope, path);
			}
		}

		private void CleanProgramFilesx86()
		{
			var path = @"C:\\Program Files (x86)\\New Relic\\.NET Agent";
			if (FileOperations.FileOrDirectoryExists(MgmtScope, path))
			{
				FileOperations.DeleteFileOrDirectory(MgmtScope, path);
			}
		}

		private void CleanProgramData()
		{
			var path = @"C:\\ProgramData\\New Relic\\.NET Agent";
			if (FileOperations.FileOrDirectoryExists(MgmtScope, path))
			{
				FileOperations.DeleteFileOrDirectory(MgmtScope, path);
			}
		}

		private void CleanRegistry()
		{
			DeleteRegistryKey(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\W3SVC", "Environment");
			DeleteRegistryKey(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WAS", "Environment");
		}

		private void CleanEnvironmentVariables()
		{
			DeleteEnvironmentVariable("COR_ENABLE_PROFILING");
			DeleteEnvironmentVariable("COR_PROFILER");
			DeleteEnvironmentVariable("COR_PROFILER_PATH");
			DeleteEnvironmentVariable("NEWRELIC_HOME");
			DeleteEnvironmentVariable("NEW_RELIC_HOME");
			DeleteEnvironmentVariable("NEW_RELIC_HOST");
			DeleteEnvironmentVariable("NEWRELIC_LICENSEKEY");
		}

		#endregion Install/Uninstall

		#region Config

		/// <summary>
		/// Loads an Xml file at the specified location.
		/// </summary>
		/// <param name="path">The location of the file.</param>
		/// <returns>XmlDocument</returns>
		public XmlDocument LoadXmlFile(String path)
		{
			var xmlDoc = new XmlDocument();

			if (Settings.Environment == Enumerations.EnvironmentSetting.Remote)
			{
				Common.Impersonate(() => xmlDoc.Load(path));
			}
			else
			{
				xmlDoc.Load(path);
			}

			return xmlDoc;
		}

		/// <summary>
		/// Saves an XmlDocument at the specified location
		/// </summary>
		/// <param name="xml">The XmlDocument to save.</param>
		/// <param name="path">The destination location.</param>
		public void SaveXmlFile(XmlDocument xmlDoc, String path)
		{
			if (Settings.Environment == Enumerations.EnvironmentSetting.Remote)
			{
				Common.Impersonate(() => xmlDoc.Save(path));
			}
			else
			{
				xmlDoc.Save(path);
			}
		}

		public void ModifyOrCreateXmlAttribute(String xPath, String attribute, String value, String pathToConfig = null, bool restartIIS = false)
		{
			var configPath = pathToConfig ?? $"{DataPath}\\newrelic.config";
			Common.Log($"Updating '{configPath}', setting '{attribute}' attribute to '{value}'.");
			var xmlDoc = LoadXmlFile(configPath);
			var navigator = xmlDoc.CreateNavigator();
			var xmlnsManager = new XmlNamespaceManager(navigator.NameTable);
			xmlnsManager.AddNamespace("x", "urn:newrelic-config");

			var xPathExp = XPathExpression.Compile(xPath);
			xPathExp.SetContext(xmlnsManager);

			var node = navigator.SelectSingleNode(xPathExp);
			if (node.GetAttribute(attribute, xmlnsManager.DefaultNamespace) != String.Empty)
			{
				node.MoveToAttribute(attribute, xmlnsManager.DefaultNamespace);
				node.SetValue(value);
			}
			else
			{
				node.CreateAttribute(String.Empty, attribute, xmlnsManager.DefaultNamespace, value);
			}

			SaveXmlFile(xmlDoc, configPath);

			if (restartIIS)
			{
				IISCommand("Reset");
			}
		}

		public void ModInstallAppWebConfigXML(bool agentEnabled)
		{
			var configPath = Settings.Environment == Enumerations.EnvironmentSetting.Remote
				? String.Format(@"\\{0}\C$\inetpub\wwwroot\DotNet-Functional-InstallTestApp\web.config", Settings.RemoteServers)
				: @"C:\inetpub\wwwroot\DotNet-Functional-InstallTestApp\web.config";
			var xmlDoc = LoadXmlFile(configPath);

			var attribute = xmlDoc.SelectSingleNode("//configuration/appSettings/add[@key='NewRelic.AgentEnabled']").Attributes["value"];
			attribute.Value = agentEnabled.ToString().ToLower();

			SaveXmlFile(xmlDoc, configPath);
		}

		#endregion Config
	}
}
