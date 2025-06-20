// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public class AzureFuncTool : RemoteService
    {
        private readonly bool _enableAzureFunctionMode;
        private readonly bool _inProc;

        private StringBuilder _stdOutStringBuilder = new();
        private StringBuilder _stdErrStringBuilder = new();
        private bool _outputCapturedAfterShutdown;

        public AzureFuncTool(string applicationDirectoryName, string executableName, string targetFramework, ApplicationType applicationType, bool createsPidFile = true, bool isCoreApp = false, bool publishApp = false, bool enableAzureFunctionMode = true, bool inProc = false)
            : base(applicationDirectoryName, executableName, targetFramework, applicationType, createsPidFile, isCoreApp, publishApp)
        {
            _enableAzureFunctionMode = enableAzureFunctionMode;
            _inProc = inProc;

            CaptureStandardOutput = false; // we implement our own output capture here, so don't use the base class implementation
        }

        public override void CopyToRemote()
        {
            base.CopyToRemote();

            // copy local.settings.json from the AzureFunctionApplication project to destination app folder
            var deployPath = Path.Combine(DestinationRootDirectoryPath, ApplicationDirectoryName, "local.settings.json");
            var localSettingsPath = Path.Combine(SourceApplicationDirectoryPath, "local.settings.json");

            File.Copy(localSettingsPath, deployPath, true);
        }

        protected override string GetStartInfoArgs(string arguments)
        {
            return UsesSpecificPort
                ? $"{arguments} --port {Port}"
                : arguments;
        }

        // github workflow will provide the path to func.exe in azure_func_exe_path, use that if found else default to just func.exe
        protected override string StartInfoFileName => Environment.GetEnvironmentVariable("azure_func_exe_path") ?? "func.exe";
        protected override string StartInfoWorkingDirectory => DestinationApplicationDirectoryPath;

        protected override void ConfigureRemoteProcess()
        {
            RemoteProcess.OutputDataReceived += (sender, args) => _stdOutStringBuilder.AppendFormat($"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] {{0}}{Environment.NewLine}", args.Data);
            RemoteProcess.ErrorDataReceived += (sender, args) => _stdErrStringBuilder.AppendFormat($"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] {{0}}{Environment.NewLine}", args.Data);
        }

        protected override void ConfigureRemoteProcessAfterStart()
        {
            // wait for the remote process to start, then begin reading the output
            Thread.Sleep(5000); // give the process some time to start up before we begin reading output
            if (RemoteProcess is null)
            {
                throw new InvalidOperationException("RemoteProcess is null. Ensure that the process has been started before calling this method.");
            }
            RemoteProcess.BeginOutputReadLine();
            RemoteProcess.BeginErrorReadLine();
        }

        protected override void AddCustomEnvironmentVariables(ProcessStartInfo startInfo)
        {
            if (!_enableAzureFunctionMode) // enabled by default, only set the environment variable if it's disabled
                startInfo.Environment.Add("NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED", _enableAzureFunctionMode.ToString());

            // environment variables needed by azure function instrumentation
            startInfo.Environment.Add("FUNCTIONS_WORKER_RUNTIME", _inProc ? "dotnet" : "dotnet-isolated");
            if (_inProc)
                startInfo.Environment.Add("FUNCTIONS_INPROC_NET8_ENABLED", "1");

            startInfo.Environment.Add("FUNCTIONS_EXTENSION_VERSION", "~4");
            startInfo.Environment.Add("WEBSITE_RESOURCE_GROUP", "my_resource_group");
            startInfo.Environment.Add("REGION_NAME", "my_azure_region");
            startInfo.Environment.Add("WEBSITE_SITE_NAME", AppName); // really should be the azure function app name, but for testing, this is preferred
            startInfo.Environment.Add("WEBSITE_OWNER_NAME", "subscription_id+my_resource_group-my_azure_region-Linux");
        }

        protected override void ConfigureStartInfo(ProcessStartInfo startInfo, string commandLineArguments, bool captureStandardOutput)
        {
            base.ConfigureStartInfo(startInfo, commandLineArguments, captureStandardOutput);

            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false; // must be false to redirect output
        }

        protected override void WaitForProcessToStartListening(bool _)
        {
            // When this URL returns a 200 and has "state":"Running" in the body, the app is ready
            var url = $"http://{DestinationServerName}:{Port}/admin/host/status";

            TestLogger?.WriteLine("Waiting for Azure function host to become ready...");
            var expectedBodyContent = "\"state\":\"Running\"";
            var status = WaitForUrlToRespond(url, 200, expectedBodyContent, 20, 1000);
            if (status)
                return;

            if (!RemoteProcess.HasExited)
            {
                try
                {
                    //We need to attempt to clean up the process that did not successfully start.
                    RemoteProcess.Kill();
                }
                catch (Exception)
                {
                    TestLogger?.WriteLine("[RemoteService]: WaitForAppServerToStartListening could not kill hung remote process.");
                }
            }

            CaptureOutput("[RemoteService]: WaitForAppServerToStartListening");
            Assert.Fail("Timed out waiting for Azure function host to become ready.");

        }

        private bool WaitForUrlToRespond(string url, int expectedStatusCode, string expectedBodyContent, int maxAttempts, int delayBetweenAttemptsMs)
        {
            for (var i = 0; i < maxAttempts; i++)
            {
                try
                {
                    using var client = new HttpClient();
                    var response = client.GetAsync(url).Result;
                    if (response.StatusCode == (HttpStatusCode)expectedStatusCode)
                    {
                        // check the body for "state" : "Running"
                        var body = response.Content.ReadAsStringAsync().Result;
                        if (body.Contains(expectedBodyContent))
                        {
                            TestLogger?.WriteLine("Azure function host is ready.");
                            return true;
                        }
                    }
                }
                catch
                {
                    // ignored
                }
                Thread.Sleep(delayBetweenAttemptsMs);
            }
            return false;
        }

        public override void Shutdown(bool _)
        {
            try
            {
                if (RemoteProcess is { HasExited: false })
                    RemoteProcess.Kill();
            }
            catch
            {
                // ignored
            }

            if (!_outputCapturedAfterShutdown) // this method gets called multiple times; only capture the output once
            {
                CaptureOutput("Azure Func Tool");
                _outputCapturedAfterShutdown = true;
            }
        }

        protected override void CaptureOutput(string processDescription)
        {
            TestLogger?.WriteLine("");
            TestLogger?.WriteLine($"====== {processDescription} standard output log =====");
            var stdOutLog = _stdOutStringBuilder.ToString();
            TestLogger?.WriteFormattedOutput(stdOutLog);
            TestLogger?.WriteLine($"====== {processDescription} end of standard output log =====");

            TestLogger?.WriteLine("");
            TestLogger?.WriteLine($"======  {processDescription}  standard error log =======");
            TestLogger?.WriteFormattedOutput(_stdErrStringBuilder.ToString());
            TestLogger?.WriteLine($"====== {processDescription} end of standard error log =====");
            TestLogger?.WriteLine("");
        }
    }
}
