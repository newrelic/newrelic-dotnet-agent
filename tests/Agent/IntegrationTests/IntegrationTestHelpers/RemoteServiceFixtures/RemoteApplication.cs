// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

public enum ApplicationType
{
    Bounded,
    Unbounded,
    Shared,
    Container,
    DotnetTool
}

public abstract class RemoteApplication : IDisposable
{
    #region Constant/Static

    private const int ExitWaitMilliseconds = 60_000;
    private const int OutputDrainMilliseconds = 15_000;

    private static string GetAssemblyFolderFromAssembly(Assembly assembly)
    {
#if NET
        return assembly.Location;
#else
            return assembly.CodeBase;
#endif
    }

    private static readonly string AssemblyBinPath = Path.GetDirectoryName(new Uri(GetAssemblyFolderFromAssembly(Assembly.GetExecutingAssembly())).LocalPath);

    private static readonly string RepositoryRootPath = Path.GetFullPath(Path.Combine(AssemblyBinPath, "..", "..", "..", "..", "..", "..", ".."));

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
            _sourceNewRelicHomeCoreClrDirectoryPath = GetSourceDirectoryForHomeDir(Utilities.RuntimeHomeDirName);
            return _sourceNewRelicHomeCoreClrDirectoryPath;
        }
        set
        {
            _sourceNewRelicHomeCoreClrDirectoryPath = value;
        }
    }

    // Deliberately does NOT write into _sourceNewRelicHomeCoreClrDirectoryPath -- that field is the
    // machine-arch memo for the property above ONLY. This helper is also called directly for
    // arch-override/Linux home dirs (CopyNewRelicHomeCoreClrWindowsDirectoryToRemote,
    // CopyNewRelicHomeCoreClrLinuxDirectoryToRemote); it used to write its result into that same shared
    // static field regardless of which homeDirName was passed, so whichever fixture ran first in a test
    // process (e.g. an x86-override class) silently poisoned the cache for every later machine-arch
    // fixture in that process -- they'd get the wrong-architecture native profiler copied with no error,
    // just a profiler that fails to load. Each call here recomputes fresh; the string concat is cheap
    // enough that losing memoization for the override paths costs nothing measurable.
    private static string GetSourceDirectoryForHomeDir(string homeDirName)
    {
        var homeRootPath = Environment.GetEnvironmentVariable("NR_DEV_HOMEROOT");

        if (!string.IsNullOrWhiteSpace(homeRootPath) && Directory.Exists(homeRootPath))
        {
            return Path.Combine(homeRootPath, homeDirName);
        }

        return Path.Combine(RepositoryRootPath, "src", "Agent", homeDirName);
    }

    // OTel distro mode (UseOtelDistro): the app runs under the OpenTelemetry .NET auto-instrumentation native
    // profiler + the NR distro plugin instead of the NR profiler. Recipe translated from the spike harness
    // (otel-distro-spike02 asyncproof run.ps1).
    private const string OtelHomeEnvironmentVariableName = "NR_DEV_OTEL_HOME";
    private const string OtelNativeProfilerFileName = "OpenTelemetry.AutoInstrumentation.Native.dll";
    private const string OtelProfilerGuid = "{918728DD-259F-4A6A-AC2B-B85E1B658318}";
    private const string OtelDistroPlugins = "NewRelic.OtelDistro.Plugin.Plugin, NewRelic.OtelDistro.Plugin:NewRelic.OtelDistro.Plugin.SamplerPlugin, NewRelic.OtelDistro.Plugin";

    private static readonly string[] OtelDistroStrippedEnvironmentVariablePrefixes = { "OTEL_", "COR_", "CORECLR_" };

    private static readonly string[] OtelDistroLoggedEnvironmentVariablePrefixes = { "OTEL_", "COR_", "CORECLR_", "DOTNET_", "COMPLUS_", "NEWRELIC_", "NEW_RELIC_" };

    private static readonly string[] SecretEnvironmentVariableNameMarkers = { "LICENSE", "KEY", "PASSWORD", "SECRET", "TOKEN", "HEADERS" };

    private static readonly string SourceOtelDistroPluginBinDirectoryPath = Path.Combine(RepositoryRootPath, "src", "Agent", "NewRelic", "OtelDistro", "NewRelic.OtelDistro.Plugin", "bin", Utilities.Configuration);

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

    public void Start(string commandLineArguments, Dictionary<string, string> environmentVariables, bool captureStandardOutput = false, bool doProfile = true)
    {
        PrepareForStart();

        var startInfo = new ProcessStartInfo();

        ConfigureStartInfo(startInfo, commandLineArguments, captureStandardOutput);

        Console.WriteLine($"[{DateTime.Now}] ${GetType().Name}.Start(): FileName={StartInfoFileName}, Arguments={GetStartInfoArgs(commandLineArguments)}, WorkingDirectory={StartInfoWorkingDirectory}, RedirectStandardOutput={captureStandardOutput}, RedirectStandardError={captureStandardOutput}, RedirectStandardInput={RedirectStandardInput}");

        ConfigureEnvironmentVariables(startInfo, environmentVariables, doProfile);

        RemoteProcess = new Process();
        RemoteProcess.StartInfo = startInfo;
        ConfigureRemoteProcess();

        RemoteProcess.Start();
        if (RemoteProcess == null)
        {
            throw new Exception("Process failed to start.");
        }

        ConfigureRemoteProcessAfterStart();

        CapturedOutput = new ProcessOutput(TestLogger, RemoteProcess, captureStandardOutput);

        if (RemoteProcess.HasExited && RemoteProcess.ExitCode != 0)
        {
            CaptureOutput("[RemoteService]: Start");
            throw new Exception("App server shutdown unexpectedly.");
        }

        WaitForProcessToStartListening(true);
    }

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

    private string _appName;
    public virtual string AppName
    {
        get
        {
            if (!string.IsNullOrEmpty(_appName))
            {
                return _appName;
            }

            // Use the app name specified in the all_solutions CI workflow, if it exists
            // this will only be set for the nightly scheduled runs so we can aggregate all integration test data under a single APM entity
            var envAppName = Environment.GetEnvironmentVariable("CI_NEW_RELIC_APP_NAME");
            return !string.IsNullOrWhiteSpace(envAppName) ? envAppName : "IntegrationTestAppName";
        }
        set
        {
            _appName = value;
        }
    }

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

    private bool _useOtelDistro;

    /// <summary>
    /// When true, the application launches under the OpenTelemetry .NET auto-instrumentation native profiler with the
    /// New Relic distro plugin (no New Relic profiler). Needs NR_DEV_OTEL_HOME pointing at a pristine OTel home, which
    /// is copied per fixture and never modified. Off by default; when off, nothing about the launch changes.
    /// Turning it on checks NR_DEV_OTEL_HOME right away (and again at staging), so a fixture without a usable OTel home
    /// fails in its constructor with a message naming the variable, instead of a later, unrelated shutdown error.
    /// </summary>
    public bool UseOtelDistro
    {
        get
        {
            return _useOtelDistro;
        }
        set
        {
            if (value)
            {
                if (Utilities.IsLinux)
                {
                    throw new InvalidOperationException("UseOtelDistro is Windows-only for now: the OTel distro staging and profiler paths are win-x64/win-x86.");
                }

                GetValidatedSourceOtelHomeDirectoryPath();
                GetValidatedOtelDistroPluginFilePath();
            }

            _useOtelDistro = value;
        }
    }

    private string DestinationOtelHomeDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, "otelhome"); } }

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

    private string CoreClrProfilerGuid => "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}";
    private string CorProfilerGuid => "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}";
    public virtual string ProfilerGuidOverride { get; set; } = null;

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
            case ApplicationType.DotnetTool:
                applicationsFolder = string.Empty; // No folder exists for a dotnet tool
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
        // Both waits are bounded. The parameterless WaitForExit also waits for
        // redirected output to reach EOF, which never happens when a spawned
        // child outlives this process and keeps the pipe open -- on Linux that
        // hung the whole test lane with no output at all.
        if (!RemoteProcess.HasExited && !RemoteProcess.WaitForExit(ExitWaitMilliseconds))
        {
            TestLogger?.WriteLine($"[RemoteApplication] Process {RemoteProcess.Id} did not exit within {ExitWaitMilliseconds / 1000} seconds.");
            return;
        }

        if (!Task.Run(() => RemoteProcess.WaitForExit()).Wait(OutputDrainMilliseconds))
        {
            TestLogger?.WriteLine($"[RemoteApplication] Redirected output did not drain within {OutputDrainMilliseconds / 1000} seconds; a child process is likely still holding the pipe.");
        }
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

    public virtual void Shutdown(bool force = false)
    {
        if (!IsRunning)
        {
            return;
        }

        if (force)
        {
            try
            {
                RemoteProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignored
            }
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
                        RemoteProcess.Kill(entireProcessTree: true);
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
                if (!remoteAppEvent.Set())
                {
                    throw new Exception($"Failed to signal the remote application to shut down. Channel name: {shutdownChannelName}");
                }
            }
            catch (Exception ex)
            {
                TestLogger?.WriteLine($"[RemoteApplication] FAILED sending shutdown signal to wait handle \"{shutdownChannelName}\": {ex}.");
                try
                {
                    RemoteProcess.Kill(entireProcessTree: true);
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

    /// <summary>
    /// Gets the command-line arguments for the process start information.
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns></returns>
    protected abstract string GetStartInfoArgs(string arguments);
    /// <summary>
    /// Gets the file name of the executable to start the process.
    /// </summary>
    protected abstract string StartInfoFileName { get; }
    /// <summary>
    /// Gets the working directory for the process start information.
    /// </summary>
    protected abstract string StartInfoWorkingDirectory { get; }

    /// <summary>
    /// Configures the remote process with the necessary settings or parameters.
    /// </summary>
    /// <remarks>This method is intended to be overridden in a derived class to provide specific
    /// configuration logic for the remote process. The base implementation does nothing.</remarks>
    protected virtual void ConfigureRemoteProcess() { }

    /// <summary>
    /// Applies additional configuration to the remote process immediately after it has started.
    /// </summary>
    /// <remarks>This method is intended to be overridden in a derived class to perform any additional
    /// setup or configuration required for the remote process after it has been started. The base
    /// implementation does nothing.</remarks>
    protected virtual void ConfigureRemoteProcessAfterStart() { }

    /// <summary>
    /// Captures and logs the output of a process described by the specified description.
    /// </summary>
    /// <remarks>This method writes the captured output of the process to the log using the provided
    /// description. Derived classes can override this method to customize the behavior of output
    /// capturing.</remarks>
    /// <param name="processDescription">A description of the process whose output is being captured. Cannot be null or empty.</param>
    protected virtual void CaptureOutput(string processDescription)
    {
        CapturedOutput.WriteProcessOutputToLog(processDescription);
    }

    /// <summary>
    /// Performs any necessary preparation before starting the process.
    /// </summary>
    /// <remarks>This method is intended to be overridden in derived classes to implement custom preparation
    /// logic.  The base implementation does nothing.</remarks>
    protected virtual void PrepareForStart() { }

    /// <summary>
    /// Waits for the process to start listening for incoming connections or data.
    /// </summary>
    /// <remarks>This method is intended to be overridden in derived classes to implement custom logic
    /// for detecting when the process is ready to accept connections or provide output. The behavior of this method
    /// depends on the implementation in the derived class. The base implementation does nothing.</remarks>
    /// <param name="captureStandardOutput">A value indicating whether the standard output of the process should be captured during the wait.</param>
    protected virtual void WaitForProcessToStartListening(bool captureStandardOutput) { }

    /// <summary>
    /// Adds custom environment variables to the specified <see cref="ProcessStartInfo"/>.
    /// </summary>
    /// <remarks>Override this method in a derived class to define custom environment variables  that
    /// should be included when starting a process. The base implementation does nothing.</remarks>
    /// <param name="startInfo">The <see cref="ProcessStartInfo"/> instance to which custom environment variables will be added.</param>
    protected virtual void AddCustomEnvironmentVariables(ProcessStartInfo startInfo) { }

    /// <summary>
    /// Configures the specified <see cref="ProcessStartInfo"/> instance with the provided command-line arguments
    /// and output capture settings.
    /// </summary>
    /// <remarks>This method sets up the process start information, including the executable file
    /// name, working directory, and redirection of input/output streams. The <paramref
    /// name="captureStandardOutput"/> parameter determines whether the standard output and error streams are
    /// redirected.</remarks>
    /// <param name="startInfo">The <see cref="ProcessStartInfo"/> instance to configure.</param>
    /// <param name="commandLineArguments">The command-line arguments to pass to the process.</param>
    /// <param name="captureStandardOutput"><see langword="true"/> to redirect the standard output and error streams; otherwise, <see
    /// langword="false"/>.</param>
    protected virtual void ConfigureStartInfo(ProcessStartInfo startInfo, string commandLineArguments, bool captureStandardOutput)
    {
        startInfo.Arguments = GetStartInfoArgs(commandLineArguments);
        startInfo.FileName = StartInfoFileName;
        startInfo.WorkingDirectory = StartInfoWorkingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = captureStandardOutput;
        startInfo.RedirectStandardError = captureStandardOutput;
        startInfo.RedirectStandardInput = RedirectStandardInput;
    }

    /// <summary>
    /// Configures the environment variables for a process, including profiler settings and custom overrides.
    /// </summary>
    /// <remarks>This method ensures that any existing New Relic environment variables are removed to
    /// avoid conflicts, applies profiler-specific settings if <paramref name="doProfile"/> is <see
    /// langword="true"/>, and adds or overrides environment variables from the provided <paramref
    /// name="environmentVariables"/> dictionary.  Additional environment variables specified in the
    /// <c>AdditionalEnvironmentVariables</c> collection are also applied, with existing variables being updated if
    /// necessary. Finally, custom environment variables are added through the <c>AddCustomEnvironmentVariables</c>
    /// method.</remarks>
    /// <param name="startInfo">The <see cref="ProcessStartInfo"/> object for the process being configured. Environment variables will be
    /// added or modified in its <see cref="ProcessStartInfo.EnvironmentVariables"/> collection.</param>
    /// <param name="environmentVariables">A dictionary of environment variables to add or override. Keys represent variable names, and values
    /// represent their corresponding values.</param>
    /// <param name="doProfile">A boolean value indicating whether profiler-related environment variables should be configured. <see
    /// langword="true"/> to configure profiler variables; otherwise, <see langword="false"/>.</param>
    protected void ConfigureEnvironmentVariables(ProcessStartInfo startInfo, Dictionary<string, string> environmentVariables, bool doProfile)
    {
        // Remove any existing New Relic environment variables to avoid conflicts
        RemoveNewRelicEnvironmentVariables(startInfo.EnvironmentVariables);

        if (UseOtelDistro)
        {
            ConfigureOtelDistroEnvironmentVariables(startInfo, doProfile);
        }
        else
        {
            ConfigureProfilerEnvironmentVariables(startInfo, doProfile);
        }

        // configure env vars as needed for testing environment overrides
        foreach (var envVar in environmentVariables)
        {
            startInfo.EnvironmentVariables.Add(envVar.Key, envVar.Value);
        }

        var profilerLogDirectoryPath = DefaultLogFileDirectoryPath;
        startInfo.EnvironmentVariables.Add("NEW_RELIC_PROFILER_LOG_DIRECTORY", profilerLogDirectoryPath);

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

        AddCustomEnvironmentVariables(startInfo);

        if (UseOtelDistro)
        {
            LogOtelDistroEnvironmentVariables(startInfo);
        }
    }

    /// <summary>
    /// OTel distro counterpart of <c>ConfigureProfilerEnvironmentVariables</c>: strips every inherited
    /// OTEL_*/COR_*/CORECLR_* variable and DOTNET_STARTUP_HOOKS, then attaches the OTel native profiler with the
    /// New Relic distro plugin pointed at the fixture's copied New Relic home.
    /// </summary>
    private void ConfigureOtelDistroEnvironmentVariables(ProcessStartInfo startInfo, bool doProfile)
    {
        var environment = startInfo.Environment;
        var inheritedKeys = new List<string>(environment.Keys);
        foreach (var key in inheritedKeys)
        {
            if (HasAnyPrefix(key, OtelDistroStrippedEnvironmentVariablePrefixes) || string.Equals(key, "DOTNET_STARTUP_HOOKS", StringComparison.OrdinalIgnoreCase))
            {
                environment.Remove(key);
            }
        }

        if (!doProfile)
        {
            return;
        }

        var otelHome = DestinationOtelHomeDirectoryPath;
        environment["OTEL_DOTNET_AUTO_HOME"] = otelHome;
        environment["OTEL_DOTNET_AUTO_PLUGINS"] = OtelDistroPlugins;
        environment["OTEL_DOTNET_AUTO_FAIL_FAST"] = "true";
        environment["OTEL_DOTNET_AUTO_SETUP_SDK"] = "true";
        environment["OTEL_TRACES_EXPORTER"] = "none";
        environment["OTEL_METRICS_EXPORTER"] = "none";
        environment["OTEL_LOGS_EXPORTER"] = "none";
        // Installer stand-in: the distro installer must set OTel's file-based (YAML) config off (a spike 05 requirement,
        // not yet implemented); NR's env defaults only hold with it off.
        environment["OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED"] = "false";
        environment["OTEL_LOG_LEVEL"] = "debug";
        environment["OTEL_DOTNET_AUTO_LOG_DIRECTORY"] = DefaultLogFileDirectoryPath;

        var nativeProfiler64 = Path.Combine(otelHome, "win-x64", OtelNativeProfilerFileName);
        var nativeProfiler32 = Path.Combine(otelHome, "win-x86", OtelNativeProfilerFileName);

        if (IsCoreApp)
        {
            environment["CORECLR_ENABLE_PROFILING"] = "1";
            environment["CORECLR_PROFILER"] = OtelProfilerGuid;
            environment["CORECLR_PROFILER_PATH_64"] = nativeProfiler64;
            environment["CORECLR_PROFILER_PATH_32"] = nativeProfiler32;
            environment["DOTNET_STARTUP_HOOKS"] = Path.Combine(otelHome, "net", "OpenTelemetry.AutoInstrumentation.StartupHook.dll");
            environment["DOTNET_EnableDiagnostics"] = "1";
            environment["CORECLR_NEW_RELIC_HOME"] = DestinationNewRelicHomeDirectoryPath;
        }
        else
        {
            environment["COR_ENABLE_PROFILING"] = "1";
            environment["COR_PROFILER"] = OtelProfilerGuid;
            environment["COR_PROFILER_PATH_64"] = nativeProfiler64;
            environment["COR_PROFILER_PATH_32"] = nativeProfiler32;
            environment["NEW_RELIC_HOME"] = DestinationNewRelicHomeDirectoryPath;
        }
    }

    private void LogOtelDistroEnvironmentVariables(ProcessStartInfo startInfo)
    {
        var keys = new List<string>();
        foreach (var key in startInfo.Environment.Keys)
        {
            if (HasAnyPrefix(key, OtelDistroLoggedEnvironmentVariablePrefixes))
            {
                keys.Add(key);
            }
        }

        keys.Sort(StringComparer.OrdinalIgnoreCase);

        TestLogger?.WriteLine("[RemoteApplication] OTel distro launch, profiler-related environment of the child process:");
        foreach (var key in keys)
        {
            var value = IsSecretEnvironmentVariableName(key) ? "<hidden>" : startInfo.Environment[key];
            TestLogger?.WriteLine($"[RemoteApplication]   {key}={value}");
        }
    }

    private static bool HasAnyPrefix(string key, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSecretEnvironmentVariableName(string key)
    {
        foreach (var marker in SecretEnvironmentVariableNameMarkers)
        {
            if (key.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Copies the pristine OTel home (NR_DEV_OTEL_HOME) into this fixture's working directory and drops the distro
    /// plugin plus the fixture home's NewRelic.Agent.Core/Extensions into its runtime folder. The source home is only
    /// read. Runs right after the New Relic home copy so Core comes from the same build as the agent home.
    /// </summary>
    private void StageOtelDistroHomeIfEnabled()
    {
        if (!UseOtelDistro)
        {
            return;
        }

        var sourceOtelHome = GetValidatedSourceOtelHomeDirectoryPath();

        var otelHome = DestinationOtelHomeDirectoryPath;
        CommonUtils.CopyDirectory(sourceOtelHome, otelHome);

        var runtimeDirectory = Path.Combine(otelHome, IsCoreApp ? "net" : "netfx");
        var pluginFilePath = GetValidatedOtelDistroPluginFilePath();
        var pluginBinDirectory = Path.GetDirectoryName(pluginFilePath);

        var stagedFiles = new List<string>
        {
            CopyFileForOtelDistro(pluginFilePath, runtimeDirectory),
            CopyFileForOtelDistro(Path.Combine(DestinationNewRelicHomeDirectoryPath, "NewRelic.Agent.Core.dll"), runtimeDirectory),
            CopyFileForOtelDistro(Path.Combine(DestinationNewRelicHomeDirectoryPath, "NewRelic.Agent.Extensions.dll"), runtimeDirectory)
        };

        // The process-wide registration coordinator ships next to the plugin (its project reference copies it per TFM). On
        // .NET Framework the coordinator loads itself into the default AppDomain from this staged copy's location.
        stagedFiles.Add(CopyFileForOtelDistro(Path.Combine(pluginBinDirectory, "NewRelic.OtelDistro.Coordinator.dll"), runtimeDirectory));

        foreach (var pdbName in new[] { "NewRelic.OtelDistro.Plugin.pdb", "NewRelic.OtelDistro.Coordinator.pdb" })
        {
            var pdb = Path.Combine(pluginBinDirectory, pdbName);
            if (File.Exists(pdb))
            {
                stagedFiles.Add(CopyFileForOtelDistro(pdb, runtimeDirectory));
            }
        }

        TestLogger?.WriteLine($"[RemoteApplication] OTel distro home staged from {sourceOtelHome} to {otelHome}:");
        foreach (var stagedFile in stagedFiles)
        {
            var info = new FileInfo(stagedFile);
            TestLogger?.WriteLine($"[RemoteApplication]   {info.FullName} size={info.Length} lastWriteUtc={info.LastWriteTimeUtc:O}");
        }
    }

    private static string GetValidatedSourceOtelHomeDirectoryPath()
    {
        var sourceOtelHome = Environment.GetEnvironmentVariable(OtelHomeEnvironmentVariableName);
        var sourceNativeProfiler = string.IsNullOrWhiteSpace(sourceOtelHome)
            ? OtelHomeEnvironmentVariableName + " is not set"
            : Path.Combine(sourceOtelHome, "win-x64", OtelNativeProfilerFileName);

        if (string.IsNullOrWhiteSpace(sourceOtelHome) || !File.Exists(sourceNativeProfiler))
        {
            throw new InvalidOperationException($"UseOtelDistro requires NR_DEV_OTEL_HOME to point at an OpenTelemetry .NET auto-instrumentation home (missing: {sourceNativeProfiler})");
        }

        return sourceOtelHome;
    }

    // The distro plugin build for this app's runtime (net8.0 for .NET, net462 for .NET Framework).
    private string GetValidatedOtelDistroPluginFilePath()
    {
        var pluginFilePath = Path.Combine(SourceOtelDistroPluginBinDirectoryPath, IsCoreApp ? "net8.0" : "net462", "NewRelic.OtelDistro.Plugin.dll");
        if (!File.Exists(pluginFilePath))
        {
            throw new InvalidOperationException($"UseOtelDistro requires the New Relic OTel distro plugin built for this app's runtime (missing: {pluginFilePath}). Build FullAgent.sln first.");
        }

        return pluginFilePath;
    }

    private static string CopyFileForOtelDistro(string sourceFilePath, string destinationDirectoryPath)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"UseOtelDistro staging: required file not found: {sourceFilePath}", sourceFilePath);
        }

        var destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(sourceFilePath));
        File.Copy(sourceFilePath, destinationFilePath, true);
        return destinationFilePath;
    }


    /// <summary>
    /// Removes all New Relic-related environment variables from the specified collection of environment variables.
    /// </summary>
    /// <remarks>This method removes both legacy and current New Relic environment variables,
    /// including those  related to the .NET Framework and .NET Core profiling configurations.  It is intended to
    /// ensure that no New Relic-specific settings remain in the provided environment variable collection.</remarks>
    /// <param name="environmentVariables">A <see cref="StringDictionary"/> containing the environment variables to modify.  This parameter cannot be
    /// <see langword="null"/>.</param>
    private void RemoveNewRelicEnvironmentVariables(StringDictionary environmentVariables)
    {
        environmentVariables.Remove("COR_ENABLE_PROFILING");
        environmentVariables.Remove("COR_PROFILER");
        environmentVariables.Remove("COR_PROFILER_PATH");
        environmentVariables.Remove("NEW_RELIC_HOME");
        environmentVariables.Remove("NEW_RELIC_PROFILER_LOG_DIRECTORY");
        environmentVariables.Remove("NEW_RELIC_LOG_DIRECTORY");
        environmentVariables.Remove("NEW_RELIC_LOG_LEVEL");
        environmentVariables.Remove("NEW_RELIC_LICENSE_KEY");
        environmentVariables.Remove("NEW_RELIC_HOST");
        environmentVariables.Remove("NEW_RELIC_INSTALL_PATH");
        environmentVariables.Remove("NEW_RELIC_APP_NAME");

        environmentVariables.Remove("CORECLR_ENABLE_PROFILING");
        environmentVariables.Remove("CORECLR_PROFILER");
        environmentVariables.Remove("CORECLR_PROFILER_PATH");
        environmentVariables.Remove("CORECLR_NEW_RELIC_HOME");

        environmentVariables.Remove("NEWRELIC_HOME");
        environmentVariables.Remove("NEWRELIC_PROFILER_LOG_DIRECTORY");
        environmentVariables.Remove("NEWRELIC_LOG_DIRECTORY");
        environmentVariables.Remove("NEWRELIC_LOG_LEVEL");
        environmentVariables.Remove("NEWRELIC_LICENSEKEY");
        environmentVariables.Remove("NEWRELIC_INSTALL_PATH");
        environmentVariables.Remove("CORECLR_NEWRELIC_HOME");
    }

    /// <summary>
    /// Removes custom environment variables from the specified collection.
    /// </summary>
    /// <remarks>This method is intended to be overridden in derived classes to implement custom logic for 
    /// removing specific environment variables. The base implementation does not perform any operations.</remarks>
    /// <param name="environmentVariables">A collection of environment variables represented as key-value pairs.  This method modifies the collection by
    /// removing custom entries as needed.</param>
    protected virtual void RemoveCustomEnvironmentVariables(StringDictionary environmentVariables)
    {
    }

    /// <summary>
    /// Configures the environment variables required for the New Relic profiler based on the application type.
    /// </summary>
    /// <remarks>This method sets the appropriate environment variables for either a .NET Core or .NET
    /// Framework application,  depending on the value of <c>IsCoreApp</c>. The variables include paths to the
    /// profiler and the New Relic home directory,  as well as enabling profiling.</remarks>
    /// <param name="startInfo">The <see cref="ProcessStartInfo"/> object to which the environment variables will be added.</param>
    /// <param name="doProfile">A boolean value indicating whether profiling should be enabled.  If <see langword="false"/>, no environment
    /// variables will be configured.</param>
    private void ConfigureProfilerEnvironmentVariables(ProcessStartInfo startInfo, bool doProfile)
    {
        var profilerFilePath = Path.Combine(DestinationNewRelicHomeDirectoryPath, Utilities.IsLinux ? @"libNewRelicProfiler.so" : @"NewRelic.Profiler.dll");
        var newRelicHomeDirectoryPath = DestinationNewRelicHomeDirectoryPath;

        var coreClrProfilerGuid = this.ProfilerGuidOverride ?? CoreClrProfilerGuid;
        var corProfilerGuid = this.ProfilerGuidOverride ?? CorProfilerGuid;

        if (!doProfile)
        {
            return;
        }

        if (IsCoreApp)
        {
            startInfo.EnvironmentVariables["CORECLR_ENABLE_PROFILING"] = "1";
            startInfo.EnvironmentVariables["CORECLR_PROFILER"] = coreClrProfilerGuid;
            startInfo.EnvironmentVariables["CORECLR_PROFILER_PATH"] = profilerFilePath;
            startInfo.EnvironmentVariables["CORECLR_NEWRELIC_HOME"] = newRelicHomeDirectoryPath;
        }
        else
        {
            startInfo.EnvironmentVariables["COR_ENABLE_PROFILING"] = "1";
            startInfo.EnvironmentVariables["COR_PROFILER"] = corProfilerGuid;
            startInfo.EnvironmentVariables["COR_PROFILER_PATH"] = profilerFilePath;
            startInfo.EnvironmentVariables["NEWRELIC_HOME"] = newRelicHomeDirectoryPath;
        }
    }

    protected void CopyNewRelicHomeDirectoryToRemote()
    {
        Directory.CreateDirectory(DestinationNewRelicHomeDirectoryPath);
        CommonUtils.CopyDirectory(SourceNewRelicHomeDirectoryPath, DestinationNewRelicHomeDirectoryPath);
        StageOtelDistroHomeIfEnabled();
    }

    protected void CopyNewRelicHomeCoreClrDirectoryToRemote()
    {
        Directory.CreateDirectory(DestinationNewRelicHomeDirectoryPath);
        CommonUtils.CopyDirectory(SourceNewRelicHomeCoreClrDirectoryPath, DestinationNewRelicHomeDirectoryPath);
        StageOtelDistroHomeIfEnabled();
    }

    protected void CopyNewRelicHomeCoreClrLinuxDirectoryToRemote(string arch)
    {
        Directory.CreateDirectory(DestinationNewRelicHomeDirectoryPath);
        CommonUtils.CopyDirectory(GetSourceDirectoryForHomeDir(Utilities.GetRuntimeHomeDirNameFor(arch, true)), DestinationNewRelicHomeDirectoryPath);
    }

    // Windows sibling of CopyNewRelicHomeCoreClrLinuxDirectoryToRemote -- for a fixture that overrides the
    // publish architecture (e.g. win-x86) away from the host machine's actual OS architecture, so it can't
    // rely on the machine-arch-derived Utilities.RuntimeHomeDirName either.
    protected void CopyNewRelicHomeCoreClrWindowsDirectoryToRemote(string arch)
    {
        Directory.CreateDirectory(DestinationNewRelicHomeDirectoryPath);
        CommonUtils.CopyDirectory(GetSourceDirectoryForHomeDir(Utilities.GetRuntimeHomeDirNameFor(arch, false)), DestinationNewRelicHomeDirectoryPath);
        StageOtelDistroHomeIfEnabled();
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
