using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public abstract class RemoteApplicationFixture : IDisposable
	{
		public virtual string TestSettingCategory { get { return "Default"; } }

		private Action _setupConfiguration;
		private Action _exerciseApplication;

		private Boolean _initialized;

		[NotNull]
		private readonly Object _initializeLock = new Object();

		[NotNull]
		private readonly RemoteApplication _remoteApplication;

		[NotNull]
		public RemoteApplication RemoteApplication { get { return _remoteApplication; } }

		[NotNull]
		public AgentLogFile AgentLog { get { return _remoteApplication.AgentLog; }}

		[NotNull]
		public String DestinationServerName { get { return _remoteApplication.DestinationServerName; } }

		[NotNull]
		public String Port { get { return _remoteApplication.Port; } }

		[NotNull]
		public String CommandLineArguments { get; set; }

		[NotNull]
		public String DestinationNewRelicConfigFilePath { get { return _remoteApplication.DestinationNewRelicConfigFilePath; } }

		[NotNull]
		public String DestinationApplicationDirectoryPath { get { return _remoteApplication.DestinationApplicationDirectoryPath; } }

		[NotNull]
		public String DestinationNewRelicExtensionsDirectoryPath => _remoteApplication.DestinationNewRelicExtensionsDirectoryPath;

		[NotNull]
		private readonly IDictionary<String, String> _initialNewRelicAppSettings = new Dictionary<String, String>();
		[NotNull]
		public IDictionary<String, String> InitialNewRelicAppSettings { get { return _initialNewRelicAppSettings; } }

		[CanBeNull]
		public ITestOutputHelper TestLogger { get; set; }

		public Boolean DelayKill;

		public Boolean BypassAgentConnectionErrorLineRegexCheck;

		private const int MaxTries = 2;

		public void DisableAsyncLocalCallStack()
		{
			var deletingFile = DestinationNewRelicExtensionsDirectoryPath + @"\NewRelic.Providers.CallStack.AsyncLocal.dll";
			if (File.Exists(deletingFile))
			{
				File.Delete(deletingFile);
			}
		}

		private IntegrationTestConfiguration _testConfiguration;

		public IntegrationTestConfiguration TestConfiguration
		{
			get
			{
				if (_testConfiguration == null)
				{
					_testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration(TestSettingCategory);
				}

				return _testConfiguration;
			}
		}

		protected RemoteApplicationFixture([NotNull] RemoteApplication remoteApplication)
		{
			_remoteApplication = remoteApplication;
		}

		public void Actions(Action setupConfiguration = null, Action exerciseApplication = null)
		{
			if (setupConfiguration != null)
				_setupConfiguration = setupConfiguration;

			if (exerciseApplication != null)
				_exerciseApplication = exerciseApplication;
		}

		public void AddActions(Action setupConfiguration = null, Action exerciseApplication = null)
		{
			if (setupConfiguration != null)
			{
				var oldSetupConfiguration = _setupConfiguration;
				_setupConfiguration = () =>
				{
					oldSetupConfiguration?.Invoke();
					setupConfiguration();
				};
			}

			if (exerciseApplication != null)
			{
				var oldExerciseApplication = _exerciseApplication;
				_exerciseApplication = () =>
				{
					oldExerciseApplication?.Invoke();
					exerciseApplication();
				};
			}
		}

		private void SetupConfiguration()
		{
			SetSecrets(DestinationNewRelicConfigFilePath);

			if (_setupConfiguration != null)
				_setupConfiguration();
		}

		protected void SetSecrets(string destinationNewRelicConfigFilePath)
		{
			CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(destinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "licenseKey", TestConfiguration.LicenseKey);
			CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(destinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "host", TestConfiguration.CollectorUrl);
			if (TestSettingCategory == "CSP")
			{
				var securityPoliciesToken = "ffff-ffff-ffff-ffff";
				CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(destinationNewRelicConfigFilePath, new[] { "configuration" }, "securityPoliciesToken", securityPoliciesToken);
			}
		}

		private void ExerciseApplication()
		{
			if (_exerciseApplication != null)
				_exerciseApplication();
		}

		public void Initialize()
		{
			if (_initialized)
				return;

			lock (_initializeLock)
			{
				if (_initialized)
					return;

				TestLogger?.WriteLine(RemoteApplication.AppName);

				var numberOfTries = 0;

				try
				{
					var appIsExercisedNormally = true;

					do
					{
						appIsExercisedNormally = true;

						_remoteApplication.DeleteWorkingSpace();

						_remoteApplication.CopyToRemote();
						foreach (var appSetting in InitialNewRelicAppSettings)
							_remoteApplication.AddAppSetting(appSetting.Key, appSetting.Value);

						SetupConfiguration();

						var captureStandardOutput = _remoteApplication.CaptureStandardOutputRequired;

						using (var appServerProcess = _remoteApplication.Start(CommandLineArguments, captureStandardOutput))
						{
							try
							{
								ExerciseApplication();
							}
							catch (Exception ex)
							{
								appIsExercisedNormally = false;
								TestLogger?.WriteLine("Exception occurred in try number " + (numberOfTries + 1) + " : " + ex.Message);

							}
							finally
							{
								if (!DelayKill)
								{
									_remoteApplication.Shutdown();

									if (captureStandardOutput)
									{
										using (var reader = appServerProcess.StandardOutput)
										{
											// Most of our tests run in HostedWebCore, but some don't, e.g. the self-hosted
											// WCF tests. For the HWC tests we carefully validate the console output in order
											// to detect process-level failures that may cause test flickers. For the self-
											// hosted tests, unfortunately, we just punt that.
											var log = reader.ReadToEnd();

											if (appIsExercisedNormally)
											{
												TestLogger?.WriteLine("====== LogValidator: raw child process log =====");
												TestLogger?.WriteLine(log);
												TestLogger?.WriteLine("====== LogValidator: end of raw child log  =====");
											}

											SubprocessLogValidator.ValidateHostedWebCoreConsoleOutput(log, TestLogger);
										}
									}
									else
									{
										TestLogger?.WriteLine("Note: child process is not required for log validation because it is running an application that test runner doesn't redirect its standard output.");
									}

									appServerProcess.WaitForExit();

									appIsExercisedNormally = AgentDidStartupWithoutLoggedErrors();

								}

								numberOfTries++;

							}
						}
					} while (!appIsExercisedNormally && numberOfTries < MaxTries);

					if (!appIsExercisedNormally)
					{
						TestLogger?.WriteLine($"Test App wasn't exercised normally after {MaxTries} tries.");
					}
				}
				finally
				{
					TestLogger?.WriteLine("===== Begin Agent log file =====");
					TestLogger?.WriteLine(AgentLog.GetFullLogAsString());
					TestLogger?.WriteLine("===== End of Agent log file =====");
					_initialized = true;
				}				
			}
		}


		private bool AgentDidStartupWithoutLoggedErrors()
		{

			//It is possisble that no log file is generated  and calling AgentLog property throws exception in that case.
			try
			{
				AgentLogFile agentLog = AgentLog;
				return agentLog.TryGetLogLine(AgentLogFile.AgentInvokingMethodErrorLineRegex) == null && 
					(BypassAgentConnectionErrorLineRegexCheck || agentLog.TryGetLogLine(AgentLogFile.AgentConnectionErrorLineRegex) == null);
			}
			catch (Exception ex)
			{
				TestLogger?.WriteLine("An exception occurred while checking Agent log file: " + ex.Message);
				return false;
			}
		}

		public virtual void Dispose()
		{
			_remoteApplication.Shutdown();
			_remoteApplication.Dispose();
		}
	}
}
