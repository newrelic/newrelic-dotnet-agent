// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public class AzureFuncTool : RemoteService
    {
        private readonly bool _enableAzureFunctionMode;

        public AzureFuncTool(string applicationDirectoryName, string targetFramework, ApplicationType applicationType, bool createsPidFile = true, bool isCoreApp = false, bool publishApp = false, bool enableAzureFunctionMode = true)
            : base(applicationDirectoryName, "AzureFunctionApplication.exe", targetFramework, applicationType, createsPidFile, isCoreApp, publishApp)
        {
            _enableAzureFunctionMode = enableAzureFunctionMode;
        }

        public override void CopyToRemote()
        {
            base.CopyToRemote();

            // copy local.settings.json from the AzureFunctionApplication project to destination app folder
            var deployPath = Path.Combine(DestinationRootDirectoryPath, ApplicationDirectoryName, "local.settings.json");
            var localSettingsPath = Path.Combine(SourceApplicationDirectoryPath, "local.settings.json");

            File.Copy(localSettingsPath, deployPath, true);
        }

        public override void Start(string commandLineArguments, Dictionary<string, string> environmentVariables, bool captureStandardOutput = false, bool doProfile = true)
        {
            var arguments = UsesSpecificPort
                ? $"{commandLineArguments} --port {Port}"
                : commandLineArguments;

            var profilerFilePath = Path.Combine(DestinationNewRelicHomeDirectoryPath, Utilities.IsLinux ? @"libNewRelicProfiler.so" : @"NewRelic.Profiler.dll");
            var newRelicHomeDirectoryPath = DestinationNewRelicHomeDirectoryPath;
            var profilerLogDirectoryPath = DefaultLogFileDirectoryPath;

            // github workflow will provide the path to func.exe in azure_func_exe_path, use that if found else default to just func.exe
            var funcExePath = Environment.GetEnvironmentVariable("azure_func_exe_path") ?? "func.exe";

            var startInfo = new ProcessStartInfo
            {
                Arguments = arguments,
                FileName = funcExePath,
                UseShellExecute = false,
                WorkingDirectory = DestinationApplicationDirectoryPath,
                RedirectStandardOutput = captureStandardOutput,
                RedirectStandardError = captureStandardOutput,
                RedirectStandardInput = RedirectStandardInput
            };

            Console.WriteLine($"[{DateTime.Now}] RemoteService.Start(): FileName=func, Arguments={arguments}, WorkingDirectory={DestinationApplicationDirectoryPath}, RedirectStandardOutput={captureStandardOutput}, RedirectStandardError={captureStandardOutput}, RedirectStandardInput={RedirectStandardInput}");

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

            startInfo.Environment.Add("NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED", _enableAzureFunctionMode.ToString());

            // environment variables needed by azure function instrumentation
            startInfo.Environment.Add("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
            startInfo.Environment.Add("WEBSITE_RESOURCE_GROUP", "my_resource_group");
            startInfo.Environment.Add("REGION_NAME", "my_azure_region");
            startInfo.Environment.Add("WEBSITE_SITE_NAME", AppName); // really should be the azure function app name, but for testing, this is preferred
            startInfo.Environment.Add("WEBSITE_OWNER_NAME", "subscription_id+my_resource_group-my_azure_region-Linux");

            //  set a custom environment variable so the azure func app can build the correct eventWaitHandle name
            startInfo.Environment.Add("AZURE_FUNCTION_APP_EVENT_HANDLE_PORT", Port.ToString());

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

        public override void Shutdown()
        {
            base.Shutdown();

            // the actual azure function is shutdown at this point, but the func tool is still running. We can kill it here safely.
            try
            {
                if (RemoteProcess is { HasExited: false })
                    RemoteProcess.Kill();
            }
            catch
            {
                // ignored
            }
        }
    }
}
