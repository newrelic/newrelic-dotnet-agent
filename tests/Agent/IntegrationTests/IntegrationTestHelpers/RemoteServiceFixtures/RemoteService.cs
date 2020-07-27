using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public class RemoteService : RemoteApplication
    {
        [NotNull]
        private readonly String _executableName;

        [NotNull]
        private readonly String _applicationDirectoryName;

        private readonly Boolean _createsPidFile;
        private readonly String _targetFramework;

        protected override string ApplicationDirectoryName => _applicationDirectoryName;

        protected bool IsCoreApp => _isCoreApp;

        protected override String SourceApplicationDirectoryPath
        {
            get
            {
                return Path.Combine(SourceApplicationsDirectoryPath, ApplicationDirectoryName, "bin", Utilities.Configuration,
                        _targetFramework) ?? String.Empty;
            }
        }

        [NotNull]
        private String DestinationApplicationExecutablePath => Path.Combine(DestinationApplicationDirectoryPath, _executableName);

        public RemoteService([NotNull] String applicationDirectoryName, [NotNull] String executableName, ApplicationType applicationType, Boolean createsPidFile = true, Boolean isCoreApp = false) : base(applicationType, isCoreApp)
        {
            _applicationDirectoryName = applicationDirectoryName;
            _executableName = executableName;
            _createsPidFile = createsPidFile;
            _targetFramework = String.Empty;

            CaptureStandardOutputRequired = false;
        }

        public RemoteService([NotNull] String applicationDirectoryName, [NotNull] String executableName, [CanBeNull] String targetFramework, ApplicationType applicationType, Boolean createsPidFile = true, Boolean isCoreApp = false) : base(applicationType, isCoreApp)
        {
            _applicationDirectoryName = applicationDirectoryName;
            _executableName = executableName;
            _createsPidFile = createsPidFile;
            _targetFramework = targetFramework ?? String.Empty;
        }
        public override void CopyToRemote()
        {
            if (IsCoreApp)
            {
                PublishCoreApp();
                CopyNewRelicHomeCoreClrDirectoryToRemote();
            }
            else
            {
                CopyNewRelicHomeDirectoryToRemote();
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "instrumentation", "applications", "application" }, "name", _executableName);
                CopyApplicationDirectoryToRemote();
            }

            SetNewRelicAppNameInExecutableConfig();
            AddInstrumentationPoint("CommandLineParser.xml", "ToBeInstrumented", "To.Be", "Instrumented");
            ModifyNewRelicConfig();
        }

        private void PublishCoreApp()
        {
            var projectFile = Path.Combine(SourceApplicationsDirectoryPath, ApplicationDirectoryName,
                ApplicationDirectoryName + ".csproj");
            var deployPath = Path.Combine(DestinationRootDirectoryPath, ApplicationDirectoryName);

            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "dotnet.exe";

            startInfo.Arguments =
                $"publish {projectFile} --configuration Release --runtime win10-x64 --framework netcoreapp2.0 --output {deployPath}";
            process.StartInfo = startInfo;
            process.Start();

            process.WaitForExit();

            Console.WriteLine($"[{DateTime.Now}] dotnet.exe exits with code {process.ExitCode}");

            if (process.ExitCode != 0)
            {
                throw new Exception("Failed to publish Core application");
            }

            Console.WriteLine($"[{DateTime.Now}] Successfully published {projectFile} to {deployPath}");
        }

        public override Process Start(String commandLineArguments, bool captureStandardOutput = false, bool doProfile = true)
        {
            var arguments = $"--port={Port} {commandLineArguments}";
            var applicationFilePath = DestinationApplicationExecutablePath;
            var profilerFilePath = Path.Combine(DestinationNewRelicHomeDirectoryPath, @"NewRelic.Profiler.dll");
            var newRelicHomeDirectoryPath = DestinationNewRelicHomeDirectoryPath;
            var profilerLogDirectoryPath = Path.Combine(DestinationNewRelicHomeDirectoryPath, @"Logs");

            var startInfo = new ProcessStartInfo
            {
                Arguments = arguments,
                FileName = applicationFilePath,
                UseShellExecute = false,
                WorkingDirectory = DestinationApplicationDirectoryPath,
                RedirectStandardOutput = captureStandardOutput,
            };

            startInfo.EnvironmentVariables.Remove("COR_ENABLE_PROFILING");
            startInfo.EnvironmentVariables.Remove("COR_PROFILER");
            startInfo.EnvironmentVariables.Remove("COR_PROFILER_PATH");
            startInfo.EnvironmentVariables.Remove("NEWRELIC_HOME");
            startInfo.EnvironmentVariables.Remove("NEWRELIC_PROFILER_LOG_DIRECTORY");
            startInfo.EnvironmentVariables.Remove("NEWRELIC_LICENSEKEY");
            startInfo.EnvironmentVariables.Remove("NEW_RELIC_LICENSE_KEY");
            startInfo.EnvironmentVariables.Remove("NEW_RELIC_HOST");

            startInfo.EnvironmentVariables.Remove("CORECLR_ENABLE_PROFILING");
            startInfo.EnvironmentVariables.Remove("CORECLR_PROFILER");
            startInfo.EnvironmentVariables.Remove("CORECLR_PROFILER_PATH");
            startInfo.EnvironmentVariables.Remove("CORECLR_NEWRELIC_HOME");

            if (!doProfile)
            {
                startInfo.EnvironmentVariables.Add("CORECLR_ENABLE_PROFILING", "0");
            }
            else if (IsCoreApp)
            {
                startInfo.EnvironmentVariables.Add("CORECLR_ENABLE_PROFILING", "1");
                startInfo.EnvironmentVariables.Add("CORECLR_PROFILER", "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}");
                startInfo.EnvironmentVariables.Add("CORECLR_PROFILER_PATH", profilerFilePath);
                startInfo.EnvironmentVariables.Add("CORECLR_NEWRELIC_HOME", newRelicHomeDirectoryPath);
            }
            else
            {
                startInfo.EnvironmentVariables.Add("COR_ENABLE_PROFILING", "1");
                startInfo.EnvironmentVariables.Add("COR_PROFILER", "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}");
                startInfo.EnvironmentVariables.Add("COR_PROFILER_PATH", profilerFilePath);
                startInfo.EnvironmentVariables.Add("NEWRELIC_HOME", newRelicHomeDirectoryPath);
            }



            startInfo.EnvironmentVariables.Add("NEWRELIC_PROFILER_LOG_DIRECTORY", profilerLogDirectoryPath);



            Process process = Process.Start(startInfo);

            if (process == null)
            {
                throw new Exception("Process failed to start.");
            }

            if (process.HasExited && process.ExitCode != 0)
            {
                throw new Exception("App server shutdown unexpectedly.");
            }

            WaitForAppServerToStartListening();

            RemoteProcessId = Convert.ToUInt32(process.Id);

            return process;
        }

        private void WaitForAppServerToStartListening()
        {
            if (!_createsPidFile)
            {
                return;
            }

            var pidFilePath = DestinationApplicationExecutablePath + ".pid";
            Console.Write("[" + DateTime.Now + "] Waiting for process to start (" + pidFilePath + ") ... " + Environment.NewLine);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < Timing.TimeToColdStart)
            {
                if (File.Exists(pidFilePath))
                {
                    Console.WriteLine("[" + DateTime.Now + "] PID file found...");
                    return;
                }
                Thread.Sleep(Timing.TimeBetweenFileExistChecks);
            }

            Assert.True(false, "Remote process never generated a .pid file!");
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
                    CommonUtils.SetAppNameInAppConfig(appConfigFile, AppName);
                }
            }
        }
    }
}
