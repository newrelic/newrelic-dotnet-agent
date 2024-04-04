// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public class DotnetTool : RemoteApplication
    {
        private readonly string _packageName;
        private readonly string _toolName;
        private readonly string _workingDirectory;

        protected override string SourceApplicationDirectoryPath => string.Empty;

        protected override string ApplicationDirectoryName => string.Empty;

        public DotnetTool(string packageName, string toolName, string workingDirectory) : base(ApplicationType.DotnetTool, true)
        {
            _packageName = packageName;
            _toolName = toolName;
            _workingDirectory = workingDirectory;
        }

        public override void CopyToRemote()
        {
            PublishWithDotnetExe();
        }

        private void PublishWithDotnetExe()
        {
            var deployPath = _workingDirectory;

            TestLogger?.WriteLine($"[DotnetTool]: Publishing to {deployPath}.");

            var sw = new Stopwatch();
            sw.Start();

            var process = new Process();
            var startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.FileName = "dotnet";
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = deployPath;

            startInfo.Arguments =
                $"tool install {_packageName} --local --create-manifest-if-needed";
            TestLogger?.WriteLine($"[DotnetTool]: executing 'dotnet {startInfo.Arguments}'");
            process.StartInfo = startInfo;

            process.Start();

            var processOutput = new ProcessOutput(TestLogger, process, true);

            // Publishes take longer in CI currently, regularly taking longer than 3 minutes.
            // 10 minutes may or may not be extreme but stabilizes these failures.
            const int timeoutInMilliseconds = 10 * 60 * 1000;
            if (!process.WaitForExit(timeoutInMilliseconds))
            {
                TestLogger?.WriteLine($"[DotetTool]: installing dotnet tool timed out while waiting for {_packageName} to install after {timeoutInMilliseconds} milliseconds.");
                try
                {
                    //This usually happens because another publishing job has a lock on the file(s) being copied.
                    //We send a termination request because we no longer want dotnet tool install to continue to copy files
                    //when there's a good chance that at least some of the files are missing.
                    //We can only use "kill" to request termination here, because there isn't a "close" option for non-GUI apps.
                    process.Kill();
                }
                catch (Exception e)
                {
                    TestLogger?.WriteLine($"======[DotnetTool]: installing dotnet tool failed to kill process that installs {_packageName} with exception =====");
                    TestLogger?.WriteLine(e.ToString());
                    TestLogger?.WriteLine($"-----[DotnetTool]: installing dotnet tool failed to kill process that installs {_packageName} end of exception -----");
                }
            }
            else
            {
                Console.WriteLine($"[DotnetTool]: [{DateTime.Now}] dotnet.exe exits with code {process.ExitCode}");
            }

            processOutput.WriteProcessOutputToLog("[DotnetTool]: installing dotnet tool");

            if (!process.HasExited || process.ExitCode != 0)
            {
                var failedToPublishMessage = "Failed to install dotnet tool";

                TestLogger?.WriteLine($"[DotnetTool]: {failedToPublishMessage}");
                throw new Exception(failedToPublishMessage);
            }

            sw.Stop();
            Console.WriteLine($"[DotnetTool]: [{DateTime.Now}] Successfully installed dotnet tool {_packageName} to {deployPath} in {sw.Elapsed}");
        }

        public override void Start(string commandLineArguments, Dictionary<string, string> environmentVariables, bool captureStandardOutput = false, bool doProfile = true)
        {
            var arguments = $"{_toolName} {commandLineArguments}";

            var applicationFilePath = "dotnet";

            var startInfo = new ProcessStartInfo
            {
                Arguments = arguments,
                FileName = applicationFilePath,
                UseShellExecute = false,
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = captureStandardOutput,
                RedirectStandardError = captureStandardOutput,
                RedirectStandardInput = RedirectStandardInput
            };

            Console.WriteLine($"[{DateTime.Now}] DotnetTool.Start(): FileName={applicationFilePath}, Arguments={arguments}, WorkingDirectory={_workingDirectory}, RedirectStandardOutput={captureStandardOutput}, RedirectStandardError={captureStandardOutput}, RedirectStandardInput={RedirectStandardInput}");

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
        }

        public override void Shutdown()
        {
            if (!IsRunning)
            {
                return;
            }

            TestLogger?.WriteLine($"[DotnetTool] Forcibly terminating dotnet tool {_toolName} process.");
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
