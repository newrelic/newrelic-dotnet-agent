// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public enum ApplicationType
    {
        Bounded,
        Unbounded,
        Shared,
        Container
    }

    public abstract class RemoteApplication : IDisposable
    {
        #region Constant/Static

        private static string GetAssemblyFolderFromAssembly(Assembly assembly)
        {
#if NET
            return assembly.Location;
#else
            return assembly.CodeBase;
#endif
        }

        private static readonly string AssemblyBinPath = Path.GetDirectoryName(new Uri(GetAssemblyFolderFromAssembly(Assembly.GetExecutingAssembly())).LocalPath);

        private static readonly string RepositoryRootPath = Path.GetFullPath(Path.Combine(AssemblyBinPath, "..", "..", "..", "..", "..","..",".."));

        protected static readonly string SourceIntegrationTestsSolutionDirectoryPath = Path.Combine(RepositoryRootPath, "tests", "Agent", "IntegrationTests");

        public readonly string SourceApplicationsDirectoryPath;

        private string _sourceNewRelicHomeDirectoryPath = string.Empty;
        private string SourceNewRelicHomeDirectoryPath
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_sourceNewRelicHomeDirectoryPath))
                {
                    return _sourceNewRelicHomeDirectoryPath;
                }

                var homeRootPath = Environment.GetEnvironmentVariable("NR_DEV_HOMEROOT");
                if (!string.IsNullOrWhiteSpace(homeRootPath) && Directory.Exists(homeRootPath))
                {
                    _sourceNewRelicHomeDirectoryPath = Path.Combine(homeRootPath, "newrelichome_x64");
                    return _sourceNewRelicHomeDirectoryPath;
                }

                _sourceNewRelicHomeDirectoryPath = Path.Combine(RepositoryRootPath, "src", "Agent", "newrelichome_x64");
                return _sourceNewRelicHomeDirectoryPath;
            }
            set
            {
                _sourceNewRelicHomeDirectoryPath = value;
            }
        }

        private static string _sourceNewRelicHomeCoreClrDirectoryPath = string.Empty;
        private static string SourceNewRelicHomeCoreClrDirectoryPath
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_sourceNewRelicHomeCoreClrDirectoryPath))
                {
                    return _sourceNewRelicHomeCoreClrDirectoryPath;
                }
                return GetSourceDirectoryForHomeDir(Utilities.RuntimeHomeDirName);
            }
            set
            {
                _sourceNewRelicHomeCoreClrDirectoryPath = value;
            }
        }

        private static string GetSourceDirectoryForHomeDir(string homeDirName)
        {
            var homeRootPath = Environment.GetEnvironmentVariable("NR_DEV_HOMEROOT");

            if (!string.IsNullOrWhiteSpace(homeRootPath) && Directory.Exists(homeRootPath))
            {
                _sourceNewRelicHomeCoreClrDirectoryPath = Path.Combine(homeRootPath, homeDirName);
                return _sourceNewRelicHomeCoreClrDirectoryPath;
            }

            _sourceNewRelicHomeCoreClrDirectoryPath = Path.Combine(RepositoryRootPath, "src", "Agent", homeDirName);
            return _sourceNewRelicHomeCoreClrDirectoryPath;
        }

        private static readonly string SourceApplicationLauncherProjectDirectoryPath = Path.Combine(SourceIntegrationTestsSolutionDirectoryPath, "ApplicationLauncher");

        private static readonly string SourceApplicationLauncherDirectoryPath = Path.Combine(SourceApplicationLauncherProjectDirectoryPath, "bin", Utilities.Configuration);

        private static string DestinationWorkingDirectoryRemotePath { get { return EnvironmentVariables.DestinationWorkingDirectoryRemotePath ?? DestinationWorkingDirectoryRemoteDefault; } }

        private static readonly string DestinationWorkingDirectoryRemoteDefault = Utilities.IsLinux ? "/tmp/IntegrationTestWorkingDirectory" : @"C:\IntegrationTestWorkingDirectory";

        #endregion

        #region Private

        private int? _port;

        public string DestinationNewRelicLogFileDirectoryPath
        {
            get
            {
                var path = CommonUtils.GetAgentLogFileDirectoryPath(DestinationNewRelicConfigFilePath);
                return path != string.Empty ? path : DefaultLogFileDirectoryPath;
            }
        }

        public string DefaultLogFileDirectoryPath
        {
            get
            {
                return Path.Combine(DestinationNewRelicHomeDirectoryPath, Utilities.IsLinux ? "logs" : "Logs");
            }
        }

        #endregion

        #region Abstract/Virtual

        /// <summary>
        /// We want to keep this as protected/private and not expose the
        /// actual process to the fixture.  This ensures that the remote application
        /// is managed internally.
        /// </summary>
        ///
        protected abstract string ApplicationDirectoryName { get; }

        protected abstract string SourceApplicationDirectoryPath { get; }

        public abstract void CopyToRemote();

        public abstract void Start(string commandLineArguments, Dictionary<string, string> environmentVariables, bool captureStandardOutput = false, bool doProfile = true);

        #endregion

        private Type _testClassType;
        public RemoteApplication SetTestClassType(Type testClassType)
        {
            _testClassType = testClassType;
            return this;
        }


        protected IDictionary<string, string> AdditionalEnvironmentVariables;
        public RemoteApplication SetAdditionalEnvironmentVariable(string key, string value)
        {
            if (AdditionalEnvironmentVariables == null)
            {
                return SetAdditionalEnvironmentVariables(new Dictionary<string, string> { { key, value } });
            }

            AdditionalEnvironmentVariables[key] = value;

            return this;
        }

        public RemoteApplication SetAdditionalEnvironmentVariables(IDictionary<string, string> envVariables)
        {
            AdditionalEnvironmentVariables = envVariables;
            return this;
        }

        protected Process RemoteProcess { get; set; }

        public virtual string AppName { get; set; } = "IntegrationTestAppName";

        private string _uniqueFolderName;
        public string UniqueFolderName
        {
            get
            {
                return _uniqueFolderName ?? (_uniqueFolderName = (_testClassType?.Name ?? ApplicationDirectoryName) + "_" + Guid.NewGuid().ToString());
            }
        }

        protected const string HostedWebCoreTargetFramework = "net462";

        public bool UseTieredCompilation { get; set; } = false;

        public bool KeepWorkingDirectory { get; set; } = false;

        protected string DestinationRootDirectoryPath { get { return Path.Combine(DestinationWorkingDirectoryRemotePath, UniqueFolderName); } }

        public string DestinationNewRelicHomeDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, "newrelichome"); } }

        public string DestinationExtensionsDirectoryPath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "extensions"); } }

        public string DestinationApplicationDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, ApplicationDirectoryName); } }

        protected string DestinationLauncherDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, "ApplicationLauncher"); } }

        protected string DestinationApplicationLauncherExecutablePath { get { return Path.Combine(DestinationLauncherDirectoryPath, HostedWebCoreTargetFramework, "ApplicationLauncher.exe"); } }

        public int Port => _port ?? (_port = RandomPortGenerator.NextPort()).Value;

        public static readonly string DestinationServerName = "127.0.0.1";

        private NewRelicConfigModifier _newRelicConfigModifier;
        public NewRelicConfigModifier NewRelicConfig => _newRelicConfigModifier ?? (_newRelicConfigModifier = new NewRelicConfigModifier(DestinationNewRelicConfigFilePath));

        public string DestinationNewRelicConfigFilePath { get { return UseLocalConfig ? DestinationLocalNewRelicConfigFilePath : DestinationGlobalNewRelicConfigFilePath; } }

        private string DestinationGlobalNewRelicConfigFilePath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "newrelic.config"); } }

        protected string DestinationLocalNewRelicConfigFilePath { get { return Path.Combine(DestinationApplicationDirectoryPath, "newrelic.config"); } }

        public string DestinationNewRelicExtensionsDirectoryPath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "extensions"); } }

        public ProfilerLogFile ProfilerLog { get { return new ProfilerLogFile(DefaultLogFileDirectoryPath, Timing.TimeToConnect); } }

        public bool CaptureStandardOutput { get; set; } = true;

        public ProcessOutput CapturedOutput { get; protected set; }

        public bool ValidateHostedWebCoreOutput { get; set; } = false;

        public ITestLogger TestLogger { get; set; }

        public bool IsCoreApp { get; }

        public bool UseLocalConfig { get; set; }

        static RemoteApplication()
        {
            AssemblySetUp.TouchMe();
        }

        protected RemoteApplication(ApplicationType applicationType, bool isCoreApp = false)
        {
            string applicationsFolder;
            switch (applicationType)
            {
                case ApplicationType.Unbounded:
                    applicationsFolder = "UnboundedApplications";
                    break;
                case ApplicationType.Shared:
                    applicationsFolder = "SharedApplications";
                    break;
                case ApplicationType.Container:
                    applicationsFolder = "ContainerApplications";
                    break;
                default:
                    applicationsFolder = "Applications";
                    break;
            }
            SourceApplicationsDirectoryPath = Path.Combine(SourceIntegrationTestsSolutionDirectoryPath, applicationsFolder);
            IsCoreApp = isCoreApp;
            if (int.TryParse(Environment.GetEnvironmentVariable("NR_DOTNET_TEST_SAVE_WORKING_DIRECTORY"), out var keepWorkingDirEnvVarValue))
            {
                KeepWorkingDirectory = (keepWorkingDirEnvVarValue == 1);
            }
        }

        public void WaitForExit()
        {
            RemoteProcess.WaitForExit();
        }

        public bool WaitForExit(int milliseconds)
        {
            if (RemoteProcess == null)
            {
                return true;
            }

            return RemoteProcess.WaitForExit(milliseconds);
        }

        public int? ExitCode => RemoteProcess.HasExited
            ? RemoteProcess.ExitCode
            : (int?)null;

        public bool IsRunning
        {
            get 
            {
                try
                {
                    return (!RemoteProcess?.HasExited) ?? false;
                }
                catch (InvalidOperationException)
                {
                    // handles Linux behavior where the process info gets cleaned up as soon as the process exits
                    return false;
                }
            }
        }   

        /// <summary>
        /// Determines if the process' standard input will be exposed and thus be manipulated.
        /// This is useful for ConsoleApps where input can be sent.
        /// </summary>
        protected bool RedirectStandardInput;

        public RemoteApplication ExposeStandardInput(bool isExposed = true)
        {
            RedirectStandardInput = isExposed;
            return this;
        }

        public void WriteToStandardInput(string commandText)
        {
            RemoteProcess.StandardInput.WriteLine(commandText);
        }

        public virtual void Shutdown()
        {
            if (!IsRunning)
            {
                return;
            }

            var shutdownChannelName = "app_server_wait_for_all_request_done_" + Port;
            
            TestLogger?.WriteLine($"[RemoteApplication] Sending shutdown signal to {ApplicationDirectoryName}.");

            if (Utilities.IsLinux)
            {
                using (NamedPipeClientStream pipeClient =
                    new NamedPipeClientStream(".", shutdownChannelName, PipeDirection.Out))
                {
                    try
                    {
                        pipeClient.Connect(1000); // 1 second connect timeout

                        using (StreamWriter sw = new StreamWriter(pipeClient))
                        {
                            sw.AutoFlush = true;
                            sw.WriteLine("Okay to shut down now");
                        }
                    }
                    catch (Exception ex)
                    {
                        TestLogger?.WriteLine($"[RemoteApplication] FAILED sending shutdown signal to named pipe \"{shutdownChannelName}\": {ex}.");
                        try
                        {
                            RemoteProcess.Kill();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
            else
            {
                try
                {
                    Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

                    //The test runner opens an event created by the app server and set it to signal the app server that the test has finished. 
                    var remoteAppEvent = EventWaitHandle.OpenExisting(shutdownChannelName);
                    remoteAppEvent.Set();
                }
                catch (Exception ex)
                {
                    TestLogger?.WriteLine($"[RemoteApplication] FAILED sending shutdown signal to wait handle \"{shutdownChannelName}\": {ex}.");
                    try
                    {
                        RemoteProcess.Kill();
                    }
                    catch
                    {
                        // ignored
                    }
                }
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
                        try
                        {
                            Directory.Delete(DestinationRootDirectoryPath, true);
                        }
                        catch (IOException)
                        {
                            Thread.Sleep(1000);
                            Directory.Delete(DestinationRootDirectoryPath, true);
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

        public void ReleasePort()
        {
            if (!_port.HasValue)
            {
                return;
            }

            RandomPortGenerator.TryReleasePort(_port.Value);
            _port = null;
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

        protected void CopyNewRelicHomeCoreClrLinuxDirectoryToRemote(string arch)
        {
            Directory.CreateDirectory(DestinationNewRelicHomeDirectoryPath);
            CommonUtils.CopyDirectory(GetSourceDirectoryForHomeDir(Utilities.GetRuntimeHomeDirNameFor(arch, true)), DestinationNewRelicHomeDirectoryPath);
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
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "requestTimeout", "60000");

        }

        public void AddInstrumentationPoint(string extensionFileName, string assemblyName, string className, string methodName, string tracerFactoryName = null)
        {
            const string extensionFileContentsTemplate =
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
            var extensionFileContents = string.Format(extensionFileContentsTemplate, tracerFactoryName, assemblyName, className, methodName);
            var extensionFilePath = Path.Combine(DestinationNewRelicExtensionsDirectoryPath, extensionFileName);
            File.WriteAllText(extensionFilePath, extensionFileContents);
        }

        public void AddAppSetting(string key, string value)
        {
            if (key == null || value == null)
                return;

            CommonUtils.SetConfigAppSetting(DestinationNewRelicConfigFilePath, key, value, "urn:newrelic-config");
        }

        public void DeleteWorkingSpace()
        {
            if (Directory.Exists(DestinationRootDirectoryPath))
            {
                File.SetAttributes(DestinationRootDirectoryPath, FileAttributes.Normal);
                try
                {
                    Directory.Delete(DestinationRootDirectoryPath, true);
                }
                catch (IOException)
                {
                    Thread.Sleep(5000);
                    Directory.Delete(DestinationRootDirectoryPath, true);
                }
            }
        }

        public class ProcessOutput
        {
            public string StandardOutput { get; set; } = string.Empty;
            public string StandardError { get; set; } = string.Empty;
            private Thread _standardOutputThread;
            private Thread _standardErrorThread;
            private readonly ITestLogger _testLogger;

            /// <summary>
            /// Class that encapsulates how to capture and retrieve both standard output and standard error
            /// from a Process while reducing the risk of deadlock. See the Microsoft documentation for
            /// Process.StandardOutput for a list of the possible deadlock risks. This class uses Microsoft's
            /// recommendation of creating two threads that can read from StandardOutput and StandardError
            /// so that those two output streams/pipes don't block each other and ultimately prevent the process
            /// from ending, which also prevents StandardOutput from reaching the end of its stream.
            /// </summary>
            /// <param name="testLogger">The logger to write the output streams to</param>
            /// <param name="process">The process with output streams to log</param>
            /// <param name="captureOutput">Flag controlling whether or not we should read and log the output and error streams for the process</param>
            public ProcessOutput(ITestLogger testLogger, Process process, bool captureOutput)
            {
                _testLogger = testLogger;

                if (captureOutput)
                {
                    StartCapturingForProcess(process);
                }
            }

            private void StartCapturingForProcess(Process process)
            {
                _standardOutputThread = new Thread(() =>
                {
                    using (var reader = process.StandardOutput)
                    {
                        StandardOutput = reader.ReadToEnd();
                    }
                })
                {
                    IsBackground = true
                };
                _standardOutputThread.Start();

                _standardErrorThread = new Thread(() =>
                {
                    using (var reader = process.StandardError)
                    {
                        StandardError = reader.ReadToEnd();
                    }
                })
                {
                    IsBackground = true
                };
                _standardErrorThread.Start();
            }

            private void WaitForOutput()
            {
                _standardOutputThread?.Join(TimeSpan.FromMinutes(2));
                _standardErrorThread?.Join(TimeSpan.FromMinutes(2));
            }

            public void WriteProcessOutputToLog(string processDescription)
            {
                WaitForOutput();

                _testLogger?.WriteLine("");
                _testLogger?.WriteLine($"====== {processDescription} standard output log =====");
                _testLogger?.WriteLine(StandardOutput);
                _testLogger?.WriteLine($"----- {processDescription} end of standard output log  -----");

                _testLogger?.WriteLine("");
                _testLogger?.WriteLine($"====== {processDescription} standard error log =====");
                _testLogger?.WriteLine(StandardError);
                _testLogger?.WriteLine($"----- {processDescription} end of standard error log -----");
                _testLogger?.WriteLine("");
            }

            public string ReturnProcessOutput()
            {
                WaitForOutput();
                return StandardOutput;
            }
        }
    }
}
