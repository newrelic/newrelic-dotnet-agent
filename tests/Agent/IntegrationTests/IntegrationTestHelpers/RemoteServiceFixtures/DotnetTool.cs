// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
public class DotnetTool : RemoteApplication
{
    private readonly string _packageName;
    private readonly string _toolName;
    private readonly string _workingDirectory;

    protected override string SourceApplicationDirectoryPath => string.Empty;

    protected override string ApplicationDirectoryName => string.Empty;

    protected override string GetStartInfoArgs(string arguments) => $"{_toolName} {arguments}";

    protected override string StartInfoFileName => "dotnet";
    protected override string StartInfoWorkingDirectory => _workingDirectory;


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

    public override void Shutdown(bool force = false)
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
