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

namespace NewRelic.Agent.ContainerIntegrationTests.Applications;

public class ContainerApplication : RemoteApplication
{
    private readonly string _dockerfile;
    private readonly string _dockerComposeFile;
    private readonly string _serviceName;
    private readonly string _dotnetVersion;
    private readonly string _distroTag;
    private readonly string _targetArch;
    private readonly string _agentArch;
    private readonly string _containerPlatform;

    private static Random random = new Random();
    private readonly long _randomId;

    // Used for handling dependent containers started automatically for services
    public readonly List<string> DockerDependencies;

    protected override string ApplicationDirectoryName { get; } = "ContainerApplication";

    protected override string SourceApplicationDirectoryPath
    {
        get
        {
            return Path.Combine(SourceApplicationsDirectoryPath, ApplicationDirectoryName);
        }
    }

    public ContainerApplication(string distroTag, Architecture containerArchitecture,
        string dotnetVersion, string dockerfile, string dockerComposeFile = "docker-compose.yml", string serviceName = "LinuxSmokeTestApp") : base(applicationType: ApplicationType.Container, isCoreApp: true)
    {
        _distroTag = distroTag;
        _dotnetVersion = dotnetVersion;
        _dockerfile = dockerfile;
        _dockerComposeFile = dockerComposeFile;
        _serviceName = serviceName;

        _randomId = random.NextInt64(); // a random id to help ensure container name uniqueness

        DockerDependencies = new List<string>();

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

    public string NRAppName =>$"ContainerTestApp_{_dotnetVersion}-{_distroTag}_{_targetArch}";

    public override string AppName => $"{NRAppName}_{_randomId}";

    private string ContainerName => AppName.ToLower().Replace(".", "_"); // must be lowercase, can't have any periods in it

    public override void CopyToRemote()
    {
        CopyNewRelicHomeCoreClrLinuxDirectoryToRemote(_agentArch);

        ModifyNewRelicConfig();
    }

    public override void Start(string commandLineArguments, Dictionary<string, string> environmentVariables, bool captureStandardOutput = false, bool doProfile = true)
    {
        CleanupContainer();

        var arguments = $"compose -f {_dockerComposeFile} -p {ContainerName} up --build --abort-on-container-exit --remove-orphans --force-recreate {_serviceName}";

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
        startInfo.EnvironmentVariables.Remove("NEW_RELIC_HOME");
        startInfo.EnvironmentVariables.Remove("NEW_RELIC_PROFILER_LOG_DIRECTORY");
        startInfo.EnvironmentVariables.Remove("NEW_RELIC_LOG_DIRECTORY");
        startInfo.EnvironmentVariables.Remove("NEW_RELIC_LOG_LEVEL");
        startInfo.EnvironmentVariables.Remove("NEW_RELIC_APP_NAME");
        startInfo.EnvironmentVariables.Remove("NEW_RELIC_LICENSE_KEY");
        startInfo.EnvironmentVariables.Remove("NEW_RELIC_HOST");
        startInfo.EnvironmentVariables.Remove("NEW_RELIC_INSTALL_PATH");
        startInfo.EnvironmentVariables.Remove("CORECLR_ENABLE_PROFILING");
        startInfo.EnvironmentVariables.Remove("CORECLR_PROFILER");
        startInfo.EnvironmentVariables.Remove("CORECLR_PROFILER_PATH");
        startInfo.EnvironmentVariables.Remove("CORECLR_NEW_RELIC_HOME");
        startInfo.EnvironmentVariables.Remove("NETWORK_NAME");

        startInfo.EnvironmentVariables.Remove("NEWRELIC_HOME");
        startInfo.EnvironmentVariables.Remove("NEWRELIC_PROFILER_LOG_DIRECTORY");
        startInfo.EnvironmentVariables.Remove("NEWRELIC_LOG_DIRECTORY");
        startInfo.EnvironmentVariables.Remove("NEWRELIC_LOG_LEVEL");
        startInfo.EnvironmentVariables.Remove("NEWRELIC_LICENSEKEY");
        startInfo.EnvironmentVariables.Remove("NEWRELIC_INSTALL_PATH");
        startInfo.EnvironmentVariables.Remove("CORECLR_NEWRELIC_HOME");

        // Docker compose settings
        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("Default");

        startInfo.EnvironmentVariables.Add("TEST_DOCKERFILE", _dockerfile);
        startInfo.EnvironmentVariables.Add("NEW_RELIC_APP_NAME", NRAppName);
        startInfo.EnvironmentVariables.Add("DOTNET_VERSION", _dotnetVersion);
        startInfo.EnvironmentVariables.Add("APP_DOTNET_VERSION", _dotnetVersion);
        startInfo.EnvironmentVariables.Add("DISTRO_TAG", _distroTag);
        startInfo.EnvironmentVariables.Add("TARGET_ARCH", _targetArch);

        startInfo.EnvironmentVariables.Add("CONTAINER_TEST_ACR_NAME", testConfiguration.DefaultSetting.ContainerTestAcrName);

        // Workflow will set BUILD_ARCH if it's a CI build
        // otherwise, assume it's a local build and set it to amd64
        if (!startInfo.EnvironmentVariables.ContainsKey("BUILD_ARCH"))
            startInfo.EnvironmentVariables.Add("BUILD_ARCH", "amd64");

        startInfo.EnvironmentVariables.Add("PLATFORM", _containerPlatform);
        startInfo.EnvironmentVariables.Add("NEW_RELIC_LICENSE_KEY", testConfiguration.LicenseKey);
        startInfo.EnvironmentVariables.Add("NEW_RELIC_HOST", testConfiguration.CollectorUrl);
        startInfo.EnvironmentVariables.Add("PORT", $"{Port}");
        startInfo.EnvironmentVariables.Add("AGENT_PATH", newRelicHomeDirectoryPath);
        startInfo.EnvironmentVariables.Add("LOG_PATH", profilerLogDirectoryPath);
        startInfo.EnvironmentVariables.Add("CONTAINER_NAME", ContainerName);

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

    public override void Shutdown(bool force = false)
    {
        if (!IsRunning)
        {
            return;
        }

        Console.WriteLine($"[{AppName} {DateTime.Now}] Sending shutdown signal to {ContainerName} container.");
        TestLogger?.WriteLine($"[{AppName}] Sending shutdown signal to {ContainerName} container.");

        // stop and remove the container, no need to kill RemoteProcess, as it will die when this command runs
        // wait up to 20 seconds for the app to terminate gracefully before forcefully closing it
        var proc = Process.Start("docker", $"compose -p {ContainerName.ToLower()} down --rmi local --remove-orphans -t 20");

        // wait for the process to complete
        if (!proc.WaitForExit(30000))
        {
            Console.WriteLine($"[{AppName} {DateTime.Now}] Timed out waiting for {ContainerName} container to stop.");
            TestLogger?.WriteLine($"[{AppName}] Timed out waiting for {ContainerName} container to stop.");
        }
    }

    private void CleanupContainer()
    {
        Console.WriteLine($"[{AppName} {DateTime.Now}] Cleaning up container and images related to {ContainerName} container.");
        TestLogger?.WriteLine($"[{AppName}] Cleaning up container and images related to {ContainerName} container.");

        Process.Start("docker", $"compose -p {ContainerName.ToLower()} down --rmi local --remove-orphans");


#if DEBUG
        // Cleanup the networks with no attached containers. Mainly for testings on dev laptops - they can build up and block runs.
        Process.Start("docker", "network prune -f");
#endif
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
