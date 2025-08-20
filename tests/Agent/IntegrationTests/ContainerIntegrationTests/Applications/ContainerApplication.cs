// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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

    private readonly string _randomToken; // short hex token (from Guid) for uniqueness
    private int _startupAttempts;

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

        _randomToken = Guid.NewGuid().ToString("N").Substring(0, 12); // 12 hex chars ensures high uniqueness across parallel runs
        _startupAttempts = 0;

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

    public string NRAppName => $"ContainerTestApp_{_dotnetVersion}-{_distroTag}_{_targetArch}";

    public override string AppName => $"{NRAppName}_{_randomToken}";

    private string ContainerName => AppName.ToLower().Replace(".", "_"); // must be lowercase, can't have any periods in it

    public override void CopyToRemote()
    {
        CopyNewRelicHomeCoreClrLinuxDirectoryToRemote(_agentArch);

        ModifyNewRelicConfig();
    }


    protected override string StartInfoFileName => "docker";
    protected override string GetStartInfoArgs(string _) => $"compose -f {_dockerComposeFile} -p {ContainerName} up --build --abort-on-container-exit --remove-orphans --force-recreate {_serviceName}";
    protected override string StartInfoWorkingDirectory => SourceApplicationsDirectoryPath;

    protected override void RemoveCustomEnvironmentVariables(StringDictionary environmentVariables)
    {
        environmentVariables.Remove("NETWORK_NAME");
    }

    protected override void AddCustomEnvironmentVariables(ProcessStartInfo startInfo)
    {
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
        startInfo.EnvironmentVariables.Add("AGENT_PATH", DestinationNewRelicHomeDirectoryPath);
        startInfo.EnvironmentVariables.Add("LOG_PATH", DefaultLogFileDirectoryPath);
        startInfo.EnvironmentVariables.Add("CONTAINER_NAME", ContainerName);
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

        try
        {
            var downProc = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose -p {ContainerName.ToLower()} down --rmi local --remove-orphans",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            downProc?.WaitForExit(30000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{AppName} {DateTime.Now}] Error during compose down: {ex.Message}");
        }

        // Force remove lingering container with same name if still present
        try
        {
            var inspect = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"ps -a --filter name=^/{ContainerName}$ -q",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            var output = inspect?.StandardOutput.ReadToEnd();
            inspect?.WaitForExit(5000);
            if (!string.IsNullOrWhiteSpace(output))
            {
                var rm = Process.Start(new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"rm -f {ContainerName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });
                rm?.WaitForExit(10000);
            }
        }
        catch { /* ignore */ }

        // Attempt removal of lingering default network (compose sometimes races on rapid successive runs)
        try
        {
            var networkName = $"{ContainerName.ToLower()}_default";
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"network rm {networkName}",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            proc?.WaitForExit(5000);
        }
        catch { /* ignore */ }


#if DEBUG
        // Cleanup the networks with no attached containers. Mainly for testings on dev laptops - they can build up and block runs.
        Process.Start("docker", "network prune -f");
#endif
    }

    protected override void PrepareForStart()
    {
        CleanupContainer();
        // Remove any stale network with expected name so compose can recreate it with correct labels
        try
        {
            var networkName = $"{ContainerName.ToLower()}_default";
            var netRm = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"network rm {networkName}",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            netRm?.WaitForExit(5000);
        }
        catch { /* ignore */ }

        CaptureDockerState("pre-start");
    }

    protected override void WaitForProcessToStartListening(bool captureStandardOutput)
    {
        Console.WriteLine($"[{AppName} {DateTime.Now}] Waiting up to {Timing.TimeToDockerComposeUp} for process to start ... ");

        var pidFilePath = Path.Combine(DefaultLogFileDirectoryPath, "containerizedapp.pid");
        var stopwatch = Stopwatch.StartNew();
        while (!RemoteProcess.HasExited && stopwatch.Elapsed < Timing.TimeToDockerComposeUp)
        {
            if (File.Exists(pidFilePath))
            {
                Console.WriteLine($"[{DateTime.Now}] PID file {pidFilePath} found...");
                CaptureDockerState("post-success");
                return;
            }
            Thread.Sleep(Timing.TimeBetweenFileExistChecks);
        }

        Console.WriteLine($"[{AppName} {DateTime.Now}] Did not find PID file matching {pidFilePath}. {(RemoteProcess.HasExited ? "Process exited" : "Wait timed out")}.");

        // Retry once if compose exited quickly (often due to transient network creation issues)
        if (RemoteProcess.HasExited && _startupAttempts == 0)
        {
            _startupAttempts++;
            Console.WriteLine($"[{AppName} {DateTime.Now}] Early compose exit detected. Retrying docker compose up (attempt {_startupAttempts}).");
            TestLogger?.WriteLine($"[{AppName}] Retrying docker compose up.");
            CleanupContainer();
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = StartInfoFileName,
                    Arguments = GetStartInfoArgs(null),
                    WorkingDirectory = StartInfoWorkingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                RemoteProcess = Process.Start(startInfo);
                WaitForProcessToStartListening(captureStandardOutput);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{AppName} {DateTime.Now}] Retry failed: {ex.Message}");
            }
        }

        if (!RemoteProcess.HasExited)
        {
            CleanupContainer();

            try
            {
                // We need to attempt to clean up the process that did not successfully start.
                RemoteProcess.Kill();
            }
            catch (Exception)
            {
                TestLogger?.WriteLine($"[{AppName}]: WaitForAppServerToStartListening could not kill hung remote process.");
            }
        }

        // Capture state after failure / before any retry
        CaptureDockerState(RemoteProcess.HasExited ? "post-failure-exited" : "post-failure-running");

        if (captureStandardOutput)
        {
            CapturedOutput.WriteProcessOutputToLog($"[{AppName}]: WaitForAppServerToStartListening");
        }

        Assert.Fail($"{AppName}: process never generated a .pid file (after {_startupAttempts + 1} attempt(s))!");
    }

    private void CaptureDockerState(string stage)
    {
        try
        {
            TestLogger?.WriteLine("");
            TestLogger?.WriteLine($"====== Docker diagnostics stage '{stage}' for {AppName} ({ContainerName}) ======");
            TestLogger?.WriteLine($"UTC: {DateTime.UtcNow:o}");

            void RunAndWrite(string title, string fileName, string args, int timeoutMs = 8000)
            {
                try
                {
                    TestLogger?.WriteLine($"--- {title}: {fileName} {args}");
                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    };
                    var p = Process.Start(psi);
                    if (p?.WaitForExit(timeoutMs) == true)
                    {
                        var stdout = p.StandardOutput.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(stdout))
                        {
                            TestLogger?.WriteLine(stdout);
                        }
                        var err = p.StandardError.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(err))
                        {
                            TestLogger?.WriteLine("[stderr]");
                            TestLogger?.WriteLine(err);
                        }
                    }
                    else
                    {
                        TestLogger?.WriteLine("(timed out)");
                    }
                }
                catch (Exception ex)
                {
                    TestLogger?.WriteLine($"(error collecting '{title}'): {ex.Message}");
                }
            }

            // Broad listing of our containers (prefix containertestapp_)
            RunAndWrite("containers", "docker", "ps -a --filter name=containertestapp_ --format \"{{.ID}} {{.Names}} {{.Status}}\"");
            // List networks
            RunAndWrite("networks", "docker", "network ls --format \"{{.ID}} {{.Name}}\"");
            // Inspect the specific expected network (may fail if absent)
            RunAndWrite("inspect_target_network", "docker", $"network inspect {ContainerName.ToLower()}_default");
            // Compose ls (if available)
            RunAndWrite("compose_projects", "docker", "compose ls");

            TestLogger?.WriteLine($"====== End docker diagnostics stage '{stage}' for {AppName} ======");
        }
        catch
        {
            // swallow logging errors
        }
    }

    public enum Architecture
    {
        X64,
        Arm64
    }
}
