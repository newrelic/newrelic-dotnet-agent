// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
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
    private readonly bool _useDynamicHostPort = true; // enable ephemeral host port mapping (Docker assigns a free port)
    private int? _resolvedHostPort; // actual host port mapped to container port 80 when using dynamic mapping

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
    // If dynamic host port allocation is enabled, set PORT env to 0 so docker chooses a free ephemeral port
    var hostPortEnv = _useDynamicHostPort ? "0" : $"{Port}";
    startInfo.EnvironmentVariables.Add("PORT", hostPortEnv);
        startInfo.EnvironmentVariables.Add("AGENT_PATH", DestinationNewRelicHomeDirectoryPath);
        startInfo.EnvironmentVariables.Add("LOG_PATH", DefaultLogFileDirectoryPath);
        startInfo.EnvironmentVariables.Add("CONTAINER_NAME", ContainerName);
    TestLogger?.WriteLine($"[{AppName}] Compose env summary: RequestedHostPort={( _useDynamicHostPort ? "dynamic(0)" : Port )}; TARGET_ARCH={_targetArch}; DISTRO_TAG={_distroTag}; DOCKERFILE={_dockerfile}; COMPOSE_FILE={_dockerComposeFile}");
    }

    public override void Shutdown(bool force = false)
    {
        if (!IsRunning)
        {
            // Even if RemoteProcess is not running we may still have lingering containers/networks (e.g. early test failure before Shutdown was invoked)
            CaptureDockerState("pre-shutdown-orphan-scan");
            CleanupContainer(includeComposeDown: false); // force removal of any leftovers
            CaptureDockerState("post-shutdown-orphan-scan");
            return;
        }

        Console.WriteLine($"[{AppName} {DateTime.Now}] Sending shutdown signal to {ContainerName} container.");
        TestLogger?.WriteLine($"[{AppName}] Sending shutdown signal to {ContainerName} container.");

        // Initiate compose down (graceful stop) allowing up to 20s for app shutdown
        var downArgs = $"compose -p {ContainerName.ToLower()} down --rmi local --remove-orphans -t 20";
        var proc = Process.Start("docker", downArgs);
        if (proc == null)
        {
            TestLogger?.WriteLine($"[{AppName}] Failed to start docker compose down process. Will attempt forced cleanup.");
        }
        else if (!proc.WaitForExit(30000))
        {
            Console.WriteLine($"[{AppName} {DateTime.Now}] Timed out waiting for compose down to complete.");
            TestLogger?.WriteLine($"[{AppName}] Timed out waiting for compose down to complete.");
        }

        // Capture state prior to forced cleanup to aid diagnostics if something lingers
        CaptureDockerState("pre-forced-cleanup");

        // Always perform forced cleanup to handle any compose race / orphan (skip second compose down within CleanupContainer)
        CleanupContainer(includeComposeDown: false);

        CaptureDockerState("post-forced-cleanup");
    }

    private void CleanupContainer(bool includeComposeDown = true)
    {
        Console.WriteLine($"[{AppName} {DateTime.Now}] Cleaning up container and images related to {ContainerName} container.");
        TestLogger?.WriteLine($"[{AppName}] Cleaning up container and images related to {ContainerName} container.");

        try
        {
            if (includeComposeDown)
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
                // Resolve actual host port if using dynamic mapping
                if (_useDynamicHostPort && _resolvedHostPort == null)
                {
                    ResolveDynamicHostPort();
                }
                return;
            }
            Thread.Sleep(Timing.TimeBetweenFileExistChecks);
        }

        Console.WriteLine($"[{AppName} {DateTime.Now}] Did not find PID file matching {pidFilePath}. {(RemoteProcess.HasExited ? "Process exited" : "Wait timed out")}.");
        if (RemoteProcess.HasExited)
        {
            TestLogger?.WriteLine($"[{AppName}] First compose attempt exited early. ExitCode={RemoteProcess.ExitCode}.");
        }

        // Retry once if compose exited quickly (often due to transient network creation issues)
        if (RemoteProcess.HasExited && _startupAttempts == 0)
        {
            _startupAttempts++;
            Console.WriteLine($"[{AppName} {DateTime.Now}] Early compose exit detected. Retrying docker compose up (attempt {_startupAttempts}).");
            TestLogger?.WriteLine($"[{AppName}] Retrying docker compose up (will attempt {_startupAttempts}).");
            // Log stdout/stderr from first attempt before retry (if captured)
            try { CapturedOutput?.WriteProcessOutputToLog($"{AppName} docker compose first-attempt"); } catch { /* ignore */ }
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

        if (RemoteProcess.HasExited)
        {
            TestLogger?.WriteLine($"[{AppName}] Final compose attempt exited. ExitCode={RemoteProcess.ExitCode}.");
            try { CapturedOutput?.WriteProcessOutputToLog($"{AppName} docker compose final-attempt"); } catch { /* ignore */ }
        }

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

            // Broad listing of our containers (prefix containertestapp_) including host ports
            RunAndWrite("containers", "docker", "ps -a --filter name=containertestapp_ --format \"{{.ID}} {{.Names}} {{.Status}} {{.Ports}}\"");
            // Focused listing for this compose project
            // For project-specific containers we need to preserve double braces in Go template; build format separately to avoid interpolation collapsing braces
            var formatTemplate = "{{.ID}} {{.Names}} {{.Status}} {{.Ports}}"; // keep double braces
            RunAndWrite("project_containers", "docker", $"ps -a --filter name={ContainerName} --format \"{formatTemplate}\"");
            // List networks
            RunAndWrite("networks", "docker", "network ls --format \"{{.ID}} {{.Name}}\"");
            // Inspect the specific expected network (may fail if absent)
            RunAndWrite("inspect_target_network", "docker", $"network inspect {ContainerName.ToLower()}_default");
            // Compose ls (if available)
            RunAndWrite("compose_projects", "docker", "compose ls");
            // Recent docker events (short window)
            RunAndWrite("recent_events", "docker", "events --since 3s --until 0s --format \"{{json .}}\"", timeoutMs: 3000);

            TestLogger?.WriteLine($"====== End docker diagnostics stage '{stage}' for {AppName} ======");
        }
        catch
        {
            // swallow logging errors
        }
    }

    private void ResolveDynamicHostPort()
    {
        try
        {
            // Prefer docker compose port command
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose -p {ContainerName} port {_serviceName} 80",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            var p = Process.Start(psi);
            if (p?.WaitForExit(5000) == true)
            {
                var output = (p.StandardOutput.ReadToEnd() + "\n" + p.StandardError.ReadToEnd()).Trim();
                // Typical outputs: "0.0.0.0:49123" or ":::49123"; extract last colon + digits
                var match = Regex.Match(output, @":(\d+)(?!.*:\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var portNum))
                {
                    _resolvedHostPort = portNum;
                    TestLogger?.WriteLine($"[{AppName}] Resolved dynamic host port: {_resolvedHostPort}");
                    return;
                }
                TestLogger?.WriteLine($"[{AppName}] Could not parse resolved host port from output: '{output}'");
            }
            else
            {
                TestLogger?.WriteLine($"[{AppName}] Timeout resolving dynamic host port.");
            }
        }
        catch (Exception ex)
        {
            TestLogger?.WriteLine($"[{AppName}] Exception resolving dynamic host port: {ex.Message}");
        }
    }

    public int EffectiveHostPort => _resolvedHostPort ?? Port; // fallback to allocated random port if not resolved
    public int? ResolvedHostPort => _resolvedHostPort; // expose raw nullable resolved port for fixtures

    public enum Architecture
    {
        X64,
        Arm64
    }
}
