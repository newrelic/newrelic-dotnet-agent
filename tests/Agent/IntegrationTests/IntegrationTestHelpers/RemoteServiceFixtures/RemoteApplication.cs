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
        if (!RemoteProcess.HasExited)
        {
            RemoteProcess.WaitForExit();
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
                RemoteProcess.Kill();
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

        ConfigureProfilerEnvironmentVariables(startInfo, doProfile);

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
