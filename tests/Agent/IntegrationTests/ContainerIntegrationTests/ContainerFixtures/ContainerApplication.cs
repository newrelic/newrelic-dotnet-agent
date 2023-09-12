// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.ContainerFixtures;

public class ContainerApplication : RemoteApplication
{

    private readonly string _dotnetVersion;
    private readonly string _distroTag;
    private readonly string _targetArch;
    private readonly string _agentArch;
    private readonly string _containerPlatform;
    private readonly string _dockerComposeServiceName;

    protected override string ApplicationDirectoryName { get; }

    protected override string SourceApplicationDirectoryPath
    {
        get
        {
            return Path.Combine(SourceApplicationsDirectoryPath, ApplicationDirectoryName);
        }
    }

    public ContainerApplication(string applicationDirectoryName, string distroTag, Architecture containerArchitecture, string dotnetVersion) : base(applicationType: ApplicationType.Container, isCoreApp: true)
    {
        ApplicationDirectoryName = applicationDirectoryName;
        _dockerComposeServiceName = applicationDirectoryName;
        _distroTag = distroTag;
        _dotnetVersion = dotnetVersion;

        switch (containerArchitecture)
        {
            case Architecture.Arm64:
                _targetArch = "arm64";
                _agentArch = "arm64";
                _containerPlatform = "linux/arm64/v8";
                break;
            default: /* x64 */
                _targetArch = "amd64";
                _agentArch = "x64";
                _containerPlatform = "linux/amd64";
                break;
        }
    }

    public override string AppName => $"ContainerApplication: {_dotnetVersion}-{_distroTag}_{_targetArch}";

    private string ContainerName => $"smoketestapp_{_dotnetVersion}-{_distroTag}_{_targetArch}".ToLower(); // must be lowercase

    public override void CopyToRemote()
    {
        CopyNewRelicHomeCoreClrLinuxDirectoryToRemote(_agentArch);

        ModifyNewRelicConfig();
    }

    public override void Start(string commandLineArguments, Dictionary<string, string> environmentVariables, bool captureStandardOutput = false, bool doProfile = true)
    {
        CleanupContainer();

        var arguments = $"compose up --force-recreate {_dockerComposeServiceName}";

        var newRelicHomeDirectoryPath = DestinationNewRelicHomeDirectoryPath;
        var profilerLogDirectoryPath = DefaultLogFileDirectoryPath;

        var startInfo = new ProcessStartInfo
        {
            Arguments = arguments,
            FileName = "docker",
            UseShellExecute = false,
            WorkingDirectory = SourceApplicationsDirectoryPath,
            RedirectStandardOutput = captureStandardOutput,
            RedirectStandardError = captureStandardOutput,
            RedirectStandardInput = RedirectStandardInput
        };

        Console.WriteLine(
            $"[{AppName} {DateTime.Now}] ContainerApplication.Start(): FileName=docker, Arguments={arguments}, WorkingDirectory={DestinationRootDirectoryPath}, RedirectStandardOutput={captureStandardOutput}, RedirectStandardError={captureStandardOutput}, RedirectStandardInput={RedirectStandardInput}");

        // Cleanup environment variables from the system
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
        startInfo.EnvironmentVariables.Remove("NETWORK_NAME");

        // Docker compose settings
        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("Default");

        startInfo.EnvironmentVariables.Add("NEW_RELIC_APP_NAME", AppName);
        startInfo.EnvironmentVariables.Add("DOTNET_VERSION", _dotnetVersion);
        startInfo.EnvironmentVariables.Add("DISTRO_TAG", _distroTag);
        startInfo.EnvironmentVariables.Add("TARGET_ARCH", _targetArch);
        startInfo.EnvironmentVariables.Add("PLATFORM", _containerPlatform);
        startInfo.EnvironmentVariables.Add("NEW_RELIC_LICENSE_KEY", testConfiguration.LicenseKey);
        startInfo.EnvironmentVariables.Add("NEW_RELIC_HOST", testConfiguration.CollectorUrl);
        startInfo.EnvironmentVariables.Add("PORT", $"{Port}");
        startInfo.EnvironmentVariables.Add("AGENT_PATH", newRelicHomeDirectoryPath);
        startInfo.EnvironmentVariables.Add("LOG_PATH", profilerLogDirectoryPath);
        startInfo.EnvironmentVariables.Add("CONTAINER_NAME", ContainerName);
        startInfo.EnvironmentVariables.Add("NETWORK_NAME", Guid.NewGuid().ToString()); // generate a random network name to keep parallel test execution from failing

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
        if (!IsRunning)
        {
            return;
        }

        Console.WriteLine($"[{AppName} {DateTime.Now}] Sending shutdown signal to {ContainerName} container.");
        TestLogger?.WriteLine($"[{AppName}] Sending shutdown signal to {ContainerName} container.");

        // stop and remove the container, no need to kill RemoteProcess, as it will die when this command runs
        // wait up to 5 seconds for the app to terminate gracefully before forcefully closing it
        Process.Start("docker", $"container stop {ContainerName} -t 5");

        Thread.Sleep(TimeSpan.FromSeconds(5)); // give things a chance to settle before destroying the container

        CleanupContainer();
    }

    private void CleanupContainer()
    {
        Console.WriteLine($"[{AppName} {DateTime.Now}] Cleaning up container and images related to {ContainerName} container.");
        // ensure there's no stray containers or images laying around
        Process.Start("docker", $"container rm --force {ContainerName}");
        Process.Start("docker", $"image rm --force {ContainerName}");
    }

    protected virtual void WaitForAppServerToStartListening(Process process, bool captureStandardOutput)
    {
        Console.WriteLine($"[{AppName} {DateTime.Now}] Waiting up to {Timing.TimeToDockerComposeUp} for process to start ... ");

        var pidFilePath = Path.Combine(DefaultLogFileDirectoryPath, "containerizedapp.pid");
        var stopwatch = Stopwatch.StartNew();
        while (!process.HasExited && stopwatch.Elapsed < Timing.TimeToDockerComposeUp)
        {
            if (File.Exists(pidFilePath))
            {
                Console.WriteLine($"[{DateTime.Now}] PID file {pidFilePath} found...");
                return;
            }
            Thread.Sleep(Timing.TimeBetweenFileExistChecks);
        }

        Console.WriteLine($"[{AppName} {DateTime.Now}] Did not find PID file matching {pidFilePath}. {(process.HasExited ? "Process exited" : "Wait timed out")}.");

        if (!process.HasExited)
        {
            CleanupContainer();

            try
            {
                // We need to attempt to clean up the process that did not successfully start.
                process.Kill();
            }
            catch (Exception)
            {
                TestLogger?.WriteLine($"[{AppName}]: WaitForAppServerToStartListening could not kill hung remote process.");
            }
        }

        if (captureStandardOutput)
        {
            CapturedOutput.WriteProcessOutputToLog($"[{AppName}]: WaitForAppServerToStartListening");
        }

        Assert.Fail($"{AppName}: process never generated a .pid file!");
    }

    public enum Architecture
    {
        X64,
        Arm64
    }
}
