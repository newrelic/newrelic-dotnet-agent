using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using JetBrains.Annotations;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
	public enum ApplicationType
	{
		Bounded,
		Unbounded
	}

	public abstract class RemoteApplication : IDisposable
	{
		#region Constant/Static
		
		[NotNull]
		private static readonly String AssemblyBinPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

		[NotNull]
		private static readonly String RepositoryRootPath = Path.Combine(AssemblyBinPath, "..", "..", "..", "..", "..", "..", "..");

		[NotNull]
		protected static readonly String SourceIntegrationTestsSolutionDirectoryPath = Path.Combine(RepositoryRootPath, "tests", "Agent", "IntegrationTests");

		[NotNull]
		protected readonly String SourceApplicationsDirectoryPath;

		[NotNull]
		private static readonly String SourceNewRelicHomeDirectoryPath = Path.Combine(RepositoryRootPath, "src", "Agent", "New Relic Home x64");

		[NotNull]
		private static readonly String SourceNewRelicHomeCoreClrDirectoryPath = Path.Combine(RepositoryRootPath, "src", "Agent", "New Relic Home x64 CoreClr");

		[NotNull]
		private static readonly String SourceApplicationLauncherProjectDirectoryPath = Path.Combine(SourceIntegrationTestsSolutionDirectoryPath, "ApplicationLauncher");

		[NotNull]
		private static readonly String SourceApplicationLauncherDirectoryPath = Path.Combine(SourceApplicationLauncherProjectDirectoryPath, "bin", Utilities.Configuration);

		[NotNull]
		private static String DestinationWorkingDirectoryRemotePath { get { return EnvironmentVariables.DestinationWorkingDirectoryRemotePath ?? DestinationWorkingDirectoryRemoteDefault; } }

		[NotNull]
		private static readonly String DestinationWorkingDirectoryRemoteDefault = Path.Combine(@"\\", Dns.GetHostName(), "C$", "IntegrationTestWorkingDirectory");

		#endregion

		#region Private

		[CanBeNull]
		private String _port;

		[NotNull]
		private String DestinationNewRelicLogFilePath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "Logs"); } }

		#endregion

		#region Abstract/Virtual

		[NotNull]
		protected abstract String ApplicationDirectoryName { get; }

		[NotNull]
		protected abstract String SourceApplicationDirectoryPath { get; }

		public abstract void CopyToRemote();

		public abstract Process Start(String commandLineArguments, bool captureStandardOutput = false, bool doProfile = true);

		public bool CaptureStandardOutputRequired { get; set; }

		#endregion

		[NotNull]
		public const String AppName = "IntegrationTestAppName";

		[NotNull]
		private readonly String _uniqueFolderName = Guid.NewGuid().ToString();

		[CanBeNull]
		protected UInt32? RemoteProcessId
		{
			get
			{
				return _remoteProcessId;
			}
			set
			{
				_remoteProcessId = value;
			}
		}
		private UInt32? _remoteProcessId;

		protected const String HostedWebCoreTargetFramework = "net451";

		public bool KeepWorkingDirectory { get; set; } = false;

		[NotNull]
		protected String DestinationRootDirectoryPath { get { return Path.Combine(DestinationWorkingDirectoryRemotePath, _uniqueFolderName); } }

		[NotNull]
		protected String DestinationNewRelicHomeDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, "New Relic Home"); } }

		[NotNull]
		public String DestinationApplicationDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, ApplicationDirectoryName); } }

		[NotNull]
		protected String DestinationLauncherDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, "ApplicationLauncher"); } }

		[NotNull]
		protected String DestinationApplicationLauncherExecutablePath { get { return Path.Combine(DestinationLauncherDirectoryPath, HostedWebCoreTargetFramework, "ApplicationLauncher.exe"); } }

		[NotNull]
		public String Port { get { return _port ?? (_port = _port = RandomPortGenerator.NextPortString()); } }

		[NotNull]
		public readonly String DestinationServerName = new Uri(DestinationWorkingDirectoryRemotePath).Host;

		[NotNull]
		public String DestinationNewRelicConfigFilePath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "newrelic.config"); } }

		[NotNull]
		public String DestinationNewRelicExtensionsDirectoryPath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "Extensions"); } }

		[NotNull]
		public AgentLogFile AgentLog { get { return new AgentLogFile(DestinationNewRelicLogFilePath, Timing.TimeToConnect); } }

		protected bool _isCoreApp; 

		static RemoteApplication()
		{
			AssemblySetUp.TouchMe();
		}

		protected RemoteApplication(ApplicationType applicationType, bool isCoreApp = false)
		{
			var applicationsFolder = applicationType == ApplicationType.Bounded
				? "Applications"
				: "UnboundedApplications";
			SourceApplicationsDirectoryPath = Path.Combine(SourceIntegrationTestsSolutionDirectoryPath, applicationsFolder);
			_isCoreApp = isCoreApp;

			var keepWorkingDirEnvVarValue = 0;
			if (int.TryParse(Environment.GetEnvironmentVariable("NR_DOTNET_TEST_SAVE_WORKING_DIRECTORY"), out keepWorkingDirEnvVarValue))
			{
				KeepWorkingDirectory = (keepWorkingDirEnvVarValue == 1);
			}
		}

		public void Shutdown()
		{
			if (!RemoteProcessId.HasValue)
				return;

			try
			{
				//The test runner opens an event created by the app server and set it to signal the app server that the test has finished. 
				var remoteAppEvent = EventWaitHandle.OpenExisting("app_server_wait_for_all_request_done_" + Port.ToString());
				remoteAppEvent.Set();
			}
			catch
			{
				ProcessExtensions.KillTreeRemote(DestinationServerName, RemoteProcessId.Value);
			}
		}

		public virtual void Dispose()
		{
			var disposed = false;
			var stopwatch = Stopwatch.StartNew();
			while (!disposed && stopwatch.Elapsed < TimeSpan.FromSeconds(30))
			{
				try
				{
					if (!KeepWorkingDirectory)
					{
						try
						{
							Directory.Delete(DestinationRootDirectoryPath, true);
						}
						catch (IOException)
						{
							Thread.Sleep(1000);
							//Directory.Delete(DestinationRootDirectoryPath, true);
							break;
						}
					}
					disposed = true;
				}
				catch (UnauthorizedAccessException)
				{
					Shutdown();
					Thread.Sleep(TimeSpan.FromSeconds(1));
				}
				catch (DirectoryNotFoundException)
				{
					return;
				}
			}
		}

		protected void CopyNewRelicHomeDirectoryToRemote()
		{
			Directory.CreateDirectory(DestinationNewRelicHomeDirectoryPath);
			CommonUtils.CopyDirectory(SourceNewRelicHomeDirectoryPath, DestinationNewRelicHomeDirectoryPath);
		}

		protected void CopyNewRelicHomeCoreClrDirectoryToRemote()
		{
			Directory.CreateDirectory(DestinationNewRelicHomeDirectoryPath);
			CommonUtils.CopyDirectory(SourceNewRelicHomeCoreClrDirectoryPath, DestinationNewRelicHomeDirectoryPath);
		}

		protected void CopyApplicationDirectoryToRemote()
		{
			Directory.CreateDirectory(DestinationApplicationDirectoryPath);
			CommonUtils.CopyDirectory(SourceApplicationDirectoryPath, DestinationApplicationDirectoryPath);
		}

		protected void CopyLauncherDirectoryToRemote()
		{
			Directory.CreateDirectory(DestinationLauncherDirectoryPath);
			CommonUtils.CopyDirectory(SourceApplicationLauncherDirectoryPath, DestinationLauncherDirectoryPath);
		}

		protected void ModifyNewRelicConfig()
		{
			CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "log" }, "level", "debug");
			CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "sendDataOnExit", "true");
			CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "sendDataOnExitThreshold", "0");
			CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "completeTransactionsOnThread", "true");
		}

		public void AddInstrumentationPoint(String extensionFileName, String assemblyName, String className, String methodName, String tracerFactoryName = null)
		{
			const String extensionFileContentsTemplate =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<extension xmlns=""urn:newrelic-extension"">
	<instrumentation>
		<tracerFactory name=""{0}"">
			<match assemblyName=""{1}"" className=""{2}"">
				<exactMethodMatcher methodName=""{3}""/>
			</match>
		</tracerFactory>
	</instrumentation>
</extension>";
			var extensionFileContents = String.Format(extensionFileContentsTemplate, tracerFactoryName, assemblyName, className, methodName);
			var extensionFilePath = Path.Combine(DestinationNewRelicExtensionsDirectoryPath, extensionFileName);
			File.WriteAllText(extensionFilePath, extensionFileContents);
		}

		public void AddAppSetting([CanBeNull] String key, [CanBeNull] String value)
		{
			if (key == null || value == null)
				return;

			var attributes = new Dictionary<String, String> {{"key", key}, {"value", value}};
			CommonUtils.ModifyOrCreateXmlAttributesInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "appSettings", "add" }, attributes);
		}

		public void DeleteWorkingSpace()
		{
			if (Directory.Exists(DestinationRootDirectoryPath))
				Directory.Delete(DestinationRootDirectoryPath, true);
		}
	}
}
