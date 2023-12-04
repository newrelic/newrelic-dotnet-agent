// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public class RemoteService : RemoteApplication
    {
        private static readonly ConcurrentDictionary<string, object> PublishCoreAppLocks = new ConcurrentDictionary<string, object>();

        protected readonly string _executableName;

        protected readonly string _applicationDirectoryName;

        private readonly bool _createsPidFile;
        private readonly string _targetFramework;

        public virtual string ProfilerGuidOverride { get; set; }

        /// <summary>
        /// Determines whether this service/application uses a port setting as an input parameter into the
        /// process.
        /// </summary>
        protected virtual bool UsesSpecificPort => true;

        protected override string ApplicationDirectoryName => _applicationDirectoryName;

        private readonly bool _publishApp;

        protected override string SourceApplicationDirectoryPath
        {
            get
            {
                return Path.Combine(SourceApplicationsDirectoryPath, ApplicationDirectoryName, "bin", Utilities.Configuration,
                        _targetFramework) ?? string.Empty;
            }
        }

        private string DestinationApplicationExecutablePath => Path.Combine(DestinationApplicationDirectoryPath, _executableName);

        public RemoteService(string applicationDirectoryName, string executableName, ApplicationType applicationType, bool createsPidFile = true, bool isCoreApp = false, bool publishApp = false) : base(applicationType, isCoreApp)
        {
            _publishApp = publishApp;
            _applicationDirectoryName = applicationDirectoryName;
            _executableName = SanitizeExecutableName(executableName);
            _createsPidFile = createsPidFile;
            _targetFramework = string.Empty;
        }

        private string SanitizeExecutableName(string executableName)
        {
            if (Utilities.IsLinux)
            {
                return executableName.Replace(".exe", "");
            }
            return executableName;
        }

        public RemoteService(string applicationDirectoryName, string executableName, string targetFramework, ApplicationType applicationType, bool createsPidFile = true, bool isCoreApp = false, bool publishApp = false) : base(applicationType, isCoreApp)
        {
            _publishApp = publishApp;
            _applicationDirectoryName = applicationDirectoryName;
            _executableName = SanitizeExecutableName(executableName);
            _createsPidFile = createsPidFile;
            _targetFramework = targetFramework ?? string.Empty;
        }

        public override void CopyToRemote()
        {
            if (IsCoreApp && _publishApp)
            {
                PublishWithDotnetExe(string.IsNullOrWhiteSpace(_targetFramework) ? "net8.0" : _targetFramework);
                CopyNewRelicHomeCoreClrDirectoryToRemote();
            }
            else if (_publishApp)
            {
                PublishWithDotnetExe(string.IsNullOrWhiteSpace(_targetFramework) ? "net462" : _targetFramework);
                CopyNewRelicHomeDirectoryToRemote();
                if (!UseLocalConfig)
                {
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "instrumentation", "applications", "application" }, "name", _executableName);
                }
            }
            else
            {
                CopyNewRelicHomeDirectoryToRemote();
                if (!UseLocalConfig)
                {
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "instrumentation", "applications", "application" }, "name", _executableName);
                }
                CopyApplicationDirectoryToRemote();
            }

            SetNewRelicAppNameInExecutableConfig();
            AddInstrumentationPoint("CommandLineParser.xml", "ToBeInstrumented", "To.Be", "Instrumented");
            ModifyNewRelicConfig();
        }

        private void PublishWithDotnetExe(string framework)
        {
            var projectFile = Path.Combine(SourceApplicationsDirectoryPath, ApplicationDirectoryName,
                ApplicationDirectoryName + ".csproj");
            var deployPath = Path.Combine(DestinationRootDirectoryPath, ApplicationDirectoryName);

            TestLogger?.WriteLine($"[RemoteService]: Publishing to {deployPath}.");

            var sw = new Stopwatch();
            sw.Start();

            var runtime = Utilities.CurrentRuntime;
            var process = new Process();
            var startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.FileName = "dotnet";
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            startInfo.Arguments =
                $"publish --configuration Release --runtime {runtime} --framework {framework} --output {deployPath} {projectFile}";
            TestLogger?.WriteLine($"[RemoteService]: executing 'dotnet {startInfo.Arguments}'");
            process.StartInfo = startInfo;

            //We cannot run dotnet publish against the same directory concurrently.
            //Doing so causes the publish job to fail because it can't obtain a lock on files in the obj and bin directories.
            lock (GetPublishLockObjectForCoreApp())
            {
                process.Start();

                var processOutput = new ProcessOutput(TestLogger, process, true);

                // Publishes take longer in CI currently, regularly taking longer than 3 minutes.
                // 10 minutes may or may not be extreme but stabilizes these failures.
                const int timeoutInMilliseconds = 10 * 60 * 1000;
                if (!process.WaitForExit(timeoutInMilliseconds))
                {
                    TestLogger?.WriteLine($"[RemoteService]: PublishCoreApp timed out while waiting for {ApplicationDirectoryName} to publish after {timeoutInMilliseconds} milliseconds.");
                    try
                    {
                        //This usually happens because another publishing job has a lock on the file(s) being copied.
                        //We send a termination request because we no longer want dotnet publish to continue to copy files
                        //when there's a good chance that at least some of the files are missing.
                        //We can only use "kill" to request termination here, because there isn't a "close" option for non-GUI apps.
                        process.Kill();
                    }
                    catch (Exception e)
                    {
                        TestLogger?.WriteLine($"======[RemoteService]: PublishCoreApp failed to kill process that publishes {ApplicationDirectoryName} with exception =====");
                        TestLogger?.WriteLine(e.ToString());
                        TestLogger?.WriteLine($"-----[RemoteService]: PublishCoreApp failed to kill process that publishes {ApplicationDirectoryName} end of exception -----");
                    }
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now}] dotnet.exe exits with code {process.ExitCode}");
                }

                processOutput.WriteProcessOutputToLog("[RemoteService]: PublishCoreApp");

                if (!process.HasExited || process.ExitCode != 0)
                {
                    var failedToPublishMessage = "Failed to publish Core application";

                    TestLogger?.WriteLine($"[RemoteService]: {failedToPublishMessage}");
                    throw new Exception(failedToPublishMessage);
                }
            }

            sw.Stop();
            Console.WriteLine($"[{DateTime.Now}] Successfully published {projectFile} to {deployPath} in {sw.Elapsed}");
        }

        private object GetPublishLockObjectForCoreApp()
        {
            return PublishCoreAppLocks.GetOrAdd(ApplicationDirectoryName, _ => new object());
        }




        public override void Start(string commandLineArguments, Dictionary<string, string> environmentVariables, bool captureStandardOutput = false, bool doProfile = true)
        {
            var arguments = UsesSpecificPort
                ? $"--port={Port} {commandLineArguments}"
                : commandLineArguments;

            var applicationFilePath = DestinationApplicationExecutablePath;
            var profilerFilePath = Path.Combine(DestinationNewRelicHomeDirectoryPath, Utilities.IsLinux ? @"libNewRelicProfiler.so" : @"NewRelic.Profiler.dll");
            var newRelicHomeDirectoryPath = DestinationNewRelicHomeDirectoryPath;
            var profilerLogDirectoryPath = DefaultLogFileDirectoryPath;

            var startInfo = new ProcessStartInfo
            {
                Arguments = arguments,
                FileName = applicationFilePath,
                UseShellExecute = false,
                WorkingDirectory = DestinationApplicationDirectoryPath,
                RedirectStandardOutput = captureStandardOutput,
                RedirectStandardError = captureStandardOutput,
                RedirectStandardInput = RedirectStandardInput
            };

            Console.WriteLine($"[{DateTime.Now}] RemoteService.Start(): FileName={applicationFilePath}, Arguments={arguments}, WorkingDirectory={DestinationApplicationDirectoryPath}, RedirectStandardOutput={captureStandardOutput}, RedirectStandardError={captureStandardOutput}, RedirectStandardInput={RedirectStandardInput}");

            startInfo.EnvironmentVariables.Remove("COR_ENABLE_PROFILING");
            startInfo.EnvironmentVariables.Remove("COR_PROFILER");
            startInfo.EnvironmentVariables.Remove("COR_PROFILER_PATH");
            startInfo.EnvironmentVariables.Remove("NEWRELIC_HOME");
            startInfo.EnvironmentVariables.Remove("NEWRELIC_PROFILER_LOG_DIRECTORY");
            startInfo.EnvironmentVariables.Remove("NEWRELIC_LOG_DIRECTORY");
            startInfo.EnvironmentVariables.Remove("NEWRELIC_LOG_LEVEL");
            startInfo.EnvironmentVariables.Remove("NEWRELIC_LICENSEKEY");
            startInfo.EnvironmentVariables.Remove("NEW_RELIC_LICENSE_KEY");
            startInfo.EnvironmentVariables.Remove("NEW_RELIC_HOST");
            startInfo.EnvironmentVariables.Remove("NEWRELIC_INSTALL_PATH");

            startInfo.EnvironmentVariables.Remove("CORECLR_ENABLE_PROFILING");
            startInfo.EnvironmentVariables.Remove("CORECLR_PROFILER");
            startInfo.EnvironmentVariables.Remove("CORECLR_PROFILER_PATH");
            startInfo.EnvironmentVariables.Remove("CORECLR_NEWRELIC_HOME");

            // configure env vars as needed for testing environment overrides
            foreach (var envVar in environmentVariables)
            {
                startInfo.EnvironmentVariables.Add(envVar.Key, envVar.Value);
            }

            if (!doProfile)
            {
                startInfo.EnvironmentVariables.Add("CORECLR_ENABLE_PROFILING", "0");
            }
            else if (IsCoreApp)
            {
                startInfo.EnvironmentVariables.Add("CORECLR_ENABLE_PROFILING", "1");
                startInfo.EnvironmentVariables.Add("CORECLR_PROFILER", this.ProfilerGuidOverride ?? "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}");
                startInfo.EnvironmentVariables.Add("CORECLR_PROFILER_PATH", profilerFilePath);
                startInfo.EnvironmentVariables.Add("CORECLR_NEWRELIC_HOME", newRelicHomeDirectoryPath);

                if (UseTieredCompilation)
                {
                    startInfo.EnvironmentVariables.Add("COMPlus_TieredCompilation", "1");
                }
            }
            else
            {
                startInfo.EnvironmentVariables.Add("COR_ENABLE_PROFILING", "1");
                startInfo.EnvironmentVariables.Add("COR_PROFILER", this.ProfilerGuidOverride ?? "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}");
                startInfo.EnvironmentVariables.Add("COR_PROFILER_PATH", profilerFilePath);
                startInfo.EnvironmentVariables.Add("NEWRELIC_HOME", newRelicHomeDirectoryPath);
            }

            startInfo.EnvironmentVariables.Add("NEWRELIC_PROFILER_LOG_DIRECTORY", profilerLogDirectoryPath);

            if (AdditionalEnvironmentVariables != null)
            {
                foreach (var kp in AdditionalEnvironmentVariables)
                {
                    if (startInfo.EnvironmentVariables.ContainsKey(kp.Key))
                        startInfo.EnvironmentVariables[kp.Key] = kp.Value;
                    else
                        startInfo.EnvironmentVariables.Add(kp.Key, kp.Value);
                }
            }

            RemoteProcess = new Process();
            RemoteProcess.StartInfo = startInfo;
            RemoteProcess.Start();

            if (RemoteProcess == null)
            {
                throw new Exception("Process failed to start.");
            }

            CapturedOutput = new ProcessOutput(TestLogger, RemoteProcess, captureStandardOutput);

            if (RemoteProcess.HasExited && RemoteProcess.ExitCode != 0)
            {
                if (captureStandardOutput)
                {
                    CapturedOutput.WriteProcessOutputToLog("[RemoteService]: Start");
                }
                throw new Exception("App server shutdown unexpectedly.");
            }

            WaitForAppServerToStartListening(RemoteProcess, captureStandardOutput);
        }

        protected virtual void WaitForAppServerToStartListening(Process process, bool captureStandardOutput)
        {
            if (!_createsPidFile)
            {
                return;
            }

            var pidFilePath = DestinationApplicationExecutablePath + ".pid";
            Console.Write("[" + DateTime.Now + "] Waiting for process to start (" + pidFilePath + ") ... " + Environment.NewLine);
            var stopwatch = Stopwatch.StartNew();
            while (!process.HasExited && stopwatch.Elapsed < Timing.TimeToColdStart)
            {
                if (File.Exists(pidFilePath))
                {
                    Console.WriteLine("[" + DateTime.Now + "] PID file " + pidFilePath + " found...");
                    return;
                }
                Thread.Sleep(Timing.TimeBetweenFileExistChecks);
            }

            if (!process.HasExited)
            {
                try
                {
                    //We need to attempt to clean up the process that did not successfully start.
                    process.Kill();
                }
                catch (Exception)
                {
                    TestLogger?.WriteLine("[RemoteService]: WaitForAppServerToStartListening could not kill hung remote process.");
                }
            }

            if (captureStandardOutput)
            {
                CapturedOutput.WriteProcessOutputToLog("[RemoteService]: WaitForAppServerToStartListening");
            }

            Assert.Fail("Remote process never generated a .pid file!");
        }

        // set the application name in app.config (agent will trump names set in newrelic.config with the process name, which is undesirable)
        private void SetNewRelicAppNameInExecutableConfig()
        {
            if (IsCoreApp)
            {
                var appSettingsFile = Path.Combine(DestinationApplicationDirectoryPath, "appsettings.json");

                if (File.Exists(appSettingsFile))
                {
                    string json = File.ReadAllText(appSettingsFile);

                    JObject jsonObj = JObject.Parse(json);
                    jsonObj.Add(new JProperty("NewRelic.AppName", AppName));

                    using (StreamWriter file = File.CreateText(appSettingsFile))
                    using (JsonTextWriter writer = new JsonTextWriter(file))
                    {
                        jsonObj.WriteTo(writer);
                    }
                }

            }
            else
            {
                var appConfigFile = Path.Combine(DestinationApplicationExecutablePath + ".config");

                if (File.Exists(appConfigFile))
                {
                    CommonUtils.SetAppNameInAppConfig(appConfigFile, AppName, null);
                }
            }
        }
    }
}
