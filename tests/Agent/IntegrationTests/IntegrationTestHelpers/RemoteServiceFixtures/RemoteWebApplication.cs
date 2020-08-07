// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public class RemoteWebApplication : RemoteApplication
    {
        private readonly string _applicationDirectoryName;

        private const string HostedWebCoreProcessName = @"HostedWebCore.exe";


        protected override string SourceApplicationDirectoryPath { get { return Path.Combine(SourceApplicationsDirectoryPath, ApplicationDirectoryName, "Deploy"); } }

        private static readonly string SourceHostedWebCoreProjectDirectoryPath = Path.Combine(SourceIntegrationTestsSolutionDirectoryPath, "HostedWebCore");

        private static readonly string SourceHostedWebCoreDirectoryPath = Path.Combine(SourceHostedWebCoreProjectDirectoryPath, "bin", Utilities.Configuration, HostedWebCoreTargetFramework);

        protected override string ApplicationDirectoryName { get { return _applicationDirectoryName; } }

        private string DestinationHostedWebCoreDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, "HostedWebCore"); } }

        private string DestinationHostedWebCoreExecutablePath { get { return Path.Combine(DestinationHostedWebCoreDirectoryPath, HostedWebCoreProcessName); } }

        private string DestinationApplicationHostConfigFilePath { get { return Path.Combine(DestinationHostedWebCoreDirectoryPath, "applicationHost.config"); } }

        private string DestinationApplicationWebConfigFilePath { get { return Path.Combine(DestinationApplicationDirectoryPath, "Web.config"); } }

        public RemoteWebApplication(string applicationDirectoryName, ApplicationType applicationType) : base(applicationType)
        {
            _applicationDirectoryName = applicationDirectoryName;

            CaptureStandardOutputRequired = true;
        }


        public override void CopyToRemote()
        {
            CopyNewRelicHomeDirectoryToRemote();
            CopyApplicationDirectoryToRemote();
            CopyHostedWebCoreToRemote();
            SetNewRelicAppNameInWebConfig();
            SetUpApplicationHostConfig();
            ModifyNewRelicConfig();

            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "instrumentation", "applications", "application" }, "name", HostedWebCoreProcessName);
        }

        public override Process Start(string commandLineArguments, bool captureStandardOutput = false, bool doProfile = true)
        {
            var arguments = $"--port={Port} {commandLineArguments}";
            var applicationFilePath = Path.Combine(DestinationHostedWebCoreDirectoryPath, "HostedWebCore.exe");
            var profilerFilePath = Path.Combine(DestinationNewRelicHomeDirectoryPath, @"NewRelic.Profiler.dll");
            var newRelicHomeDirectoryPath = DestinationNewRelicHomeDirectoryPath;
            var profilerLogDirectoryPath = Path.Combine(DestinationNewRelicHomeDirectoryPath, @"Logs");

            var startInfo = new ProcessStartInfo
            {
                Arguments = arguments,
                FileName = applicationFilePath,
                UseShellExecute = false,
                WorkingDirectory = DestinationHostedWebCoreDirectoryPath,
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

            if (!doProfile)
            {
                startInfo.EnvironmentVariables.Add("COR_ENABLE_PROFILING", "0");
            }
            else
            {
                startInfo.EnvironmentVariables.Add("COR_ENABLE_PROFILING", "1");
                startInfo.EnvironmentVariables.Add("COR_PROFILER", "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}");
                startInfo.EnvironmentVariables.Add("COR_PROFILER_PATH", profilerFilePath);
                startInfo.EnvironmentVariables.Add("NEWRELIC_HOME", newRelicHomeDirectoryPath);
                startInfo.EnvironmentVariables.Add("NEWRELIC_PROFILER_LOG_DIRECTORY", profilerLogDirectoryPath);
            }

            Process process = Process.Start(startInfo);

            if (process == null)
                throw new Exception("Process failed to start.");

            if (process.HasExited && process.ExitCode != 0)
                throw new Exception("Hosted Web Core shutdown unexpectedly.");

            WaitForHostedWebCoreToStartListening();

            RemoteProcessId = Convert.ToUInt32(process.Id);

            return process;
        }

        private void CopyHostedWebCoreToRemote()
        {
            Directory.CreateDirectory(DestinationHostedWebCoreDirectoryPath);
            CommonUtils.CopyDirectory(SourceHostedWebCoreDirectoryPath, DestinationHostedWebCoreDirectoryPath);
        }

        private void SetNewRelicAppNameInWebConfig()
        {
            var nodes = new[]
            {
                "configuration",
                "appSettings",
                "add",
            };
            var attributes = new[]
            {
                new KeyValuePair<string, string>("key", "NewRelic.AppName"),
                new KeyValuePair<string, string>("value", AppName),
            };
            CommonUtils.ModifyOrCreateXmlAttributes(DestinationApplicationWebConfigFilePath, string.Empty, nodes, attributes);
        }

        private void SetUpApplicationHostConfig()
        {
            SetSiteVirtualDirectoryInApplicationHostConfig();
            SetSitePortInApplicationHostConfig();
            SetApplicationPoolInApplicationHostConfig();
        }

        private void SetSiteVirtualDirectoryInApplicationHostConfig()
        {
            var nodes = new[]
            {
                "configuration",
                "system.applicationHost",
                "sites",
                "site",
                "application",
                "virtualDirectory",
            };
            var attributes = new[]
            {
                new KeyValuePair<string, string>("physicalPath", CommonUtils.GetLocalPathFromRemotePath(DestinationApplicationDirectoryPath)),
            };
            CommonUtils.ModifyOrCreateXmlAttributes(DestinationApplicationHostConfigFilePath, string.Empty, nodes, attributes);
        }

        private void SetSitePortInApplicationHostConfig()
        {
            var nodes = new[]
            {
                "configuration",
                "system.applicationHost",
                "sites",
                "site",
                "bindings",
                "binding",
            };
            var attributes = new[]
            {
                new KeyValuePair<string, string>("bindingInformation", string.Format(@"*:{0}:", Port)),
            };
            CommonUtils.ModifyOrCreateXmlAttributes(DestinationApplicationHostConfigFilePath, string.Empty, nodes, attributes);
        }

        private void SetApplicationPoolInApplicationHostConfig()
        {
            var appPoolName = @"IntegrationTestAppPool" + Port;

            var nodes = new[]
            {
                "configuration",
                "system.applicationHost",
                "applicationPools",
                "add",
            };
            var attributes = new[]
            {
                new KeyValuePair<string, string>("name", appPoolName),
            };
            CommonUtils.ModifyOrCreateXmlAttributes(DestinationApplicationHostConfigFilePath, string.Empty, nodes, attributes);

            nodes = new[]
            {
                "configuration",
                "system.applicationHost",
                "sites",
                "applicationDefaults",
            };
            attributes = new[]
            {
                new KeyValuePair<string, string>("applicationPool", appPoolName),
            };
            CommonUtils.ModifyOrCreateXmlAttributes(DestinationApplicationHostConfigFilePath, string.Empty, nodes, attributes);
        }

        private void WaitForHostedWebCoreToStartListening()
        {
            var pidFilePath = DestinationHostedWebCoreExecutablePath + ".pid";
            Console.Write("[" + DateTime.Now + "] Waiting for process to start (" + pidFilePath + ") ... " + Environment.NewLine);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < Timing.TimeToColdStart)
            {
                if (File.Exists(pidFilePath))
                {
                    Console.Write("Done." + Environment.NewLine);
                    return;
                }
                Thread.Sleep(Timing.TimeBetweenFileExistChecks);
            }

            Assert.True(false, "Remote process never generated a .pid file!");
        }
    }
}
