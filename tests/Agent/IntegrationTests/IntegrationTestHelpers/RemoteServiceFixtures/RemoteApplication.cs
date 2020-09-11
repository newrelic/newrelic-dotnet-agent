// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

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

        private static readonly string AssemblyBinPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

        private static readonly string RepositoryRootPath = Path.Combine(AssemblyBinPath, "..", "..", "..", "..", "..", "..", "..");

        protected static readonly string SourceIntegrationTestsSolutionDirectoryPath = Path.Combine(RepositoryRootPath, "tests", "Agent", "IntegrationTests");

        protected readonly string SourceApplicationsDirectoryPath;

        private static readonly string SourceNewRelicHomeDirectoryPath = Path.Combine(RepositoryRootPath, "src", "Agent", "newrelichome_x64");

        private static readonly string SourceNewRelicHomeCoreClrDirectoryPath = Path.Combine(RepositoryRootPath, "src", "Agent", "newrelichome_x64_coreclr");

        private static readonly string SourceApplicationLauncherProjectDirectoryPath = Path.Combine(SourceIntegrationTestsSolutionDirectoryPath, "ApplicationLauncher");

        private static readonly string SourceApplicationLauncherDirectoryPath = Path.Combine(SourceApplicationLauncherProjectDirectoryPath, "bin", Utilities.Configuration);

        private static string DestinationWorkingDirectoryRemotePath { get { return EnvironmentVariables.DestinationWorkingDirectoryRemotePath ?? DestinationWorkingDirectoryRemoteDefault; } }

        private static readonly string DestinationWorkingDirectoryRemoteDefault = Path.Combine(@"\\", Dns.GetHostName(), "C$", "IntegrationTestWorkingDirectory");

        public string DestinationExtensionsDirectoryPath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "extensions"); } }

        public string DestinationApplicationDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, ApplicationDirectoryName); } }

        protected string DestinationLauncherDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, "ApplicationLauncher"); } }

        protected string DestinationApplicationLauncherExecutablePath { get { return Path.Combine(DestinationLauncherDirectoryPath, HostedWebCoreTargetFramework, "ApplicationLauncher.exe"); } }

        public void WaitForExit()
        {
            RemoteProcess.WaitForExit();
        }

        public void Kill()
        {
            RemoteProcess?.Kill();
        }

        #endregion

        #region Private

        private string _port;

        private string DestinationNewRelicLogFilePath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "Logs"); } }

        #endregion

        #region Abstract/Virtual

        protected abstract string ApplicationDirectoryName { get; }

        protected abstract string SourceApplicationDirectoryPath { get; }

        public abstract void CopyToRemote();

        public abstract Process Start(string commandLineArguments, bool captureStandardOutput = false, bool doProfile = true);

        public bool CaptureStandardOutputRequired { get; set; }

        #endregion

        public ITestLogger TestLogger { get; set; }

        public const string AppName = "IntegrationTestAppName";

        private readonly string _uniqueFolderName = Guid.NewGuid().ToString();

        protected uint? RemoteProcessId
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
        private uint? _remoteProcessId;

        protected Process RemoteProcess { get; set; }

        public ProcessOutput CapturedOutput { get; protected set; }

        public int? ExitCode => RemoteProcess.HasExited
            ? RemoteProcess.ExitCode
            : (int?)null;


        public bool IsRunning => (!RemoteProcess?.HasExited) ?? false;

        protected const string HostedWebCoreTargetFramework = "net451";

        public bool KeepWorkingDirectory { get; set; } = false;

        protected string DestinationRootDirectoryPath { get { return Path.Combine(DestinationWorkingDirectoryRemotePath, _uniqueFolderName); } }

        public string DestinationNewRelicHomeDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, "New Relic Home"); } }

        public string Port { get { return _port ?? (_port = _port = RandomPortGenerator.NextPortString()); } }

        public readonly string DestinationServerName = new Uri(DestinationWorkingDirectoryRemotePath).Host;

        public string DestinationNewRelicConfigFilePath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "newrelic.config"); } }

        public string DestinationNewRelicExtensionsDirectoryPath { get { return Path.Combine(DestinationNewRelicHomeDirectoryPath, "Extensions"); } }

        public bool ValidateHostedWebCoreOutput { get; set; } = false;

        public AgentLogFile AgentLog { get { return new AgentLogFile(DestinationNewRelicLogFilePath, Timing.TimeToConnect); } }

        protected bool _isCoreApp;

        static RemoteApplication()
        {
            AssemblySetUp.TouchMe();
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

        public virtual void Shutdown()
        {
            if (!IsRunning)
            {
                return;
            }

            var expectedWaitHandle = "app_server_wait_for_all_request_done_" + Port;

            try
            {
                TestLogger?.WriteLine($"[RemoteApplication] Sending shutdown signal to {ApplicationDirectoryName}.");
                //The test runner opens an event created by the app server and set it to signal the app server that the test has finished. 
                var remoteAppEvent = EventWaitHandle.OpenExisting(expectedWaitHandle);
                remoteAppEvent.Set();
            }
            catch (Exception ex)
            {
                TestLogger?.WriteLine($"[RemoteApplication] FAILED sending shutdown signal to wait handle \"{expectedWaitHandle}\": {ex}.");
                RemoteProcess.Kill();
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
                            Directory.Delete(DestinationRootDirectoryPath, true);
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

            var attributes = new Dictionary<string, string> { { "key", key }, { "value", value } };
            CommonUtils.ModifyOrCreateXmlAttributesInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "appSettings", "add" }, attributes);
        }

        public void DeleteWorkingSpace()
        {
            if (Directory.Exists(DestinationRootDirectoryPath))
                Directory.Delete(DestinationRootDirectoryPath, true);
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

        public virtual void WriteProcessOutputToLog(string processDescription)
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
