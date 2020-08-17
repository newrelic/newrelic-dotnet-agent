// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.IO;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit.Abstractions;

namespace PlatformTests.Applications
{
    public abstract class BaseApplication
    {
        public string ApplicationName { get; }

        public string[] ServiceNames { get; }

        public ITestOutputHelper TestLogger { get; set; }

        private string _testSettingCategory;

        private IntegrationTestConfiguration _testConfiguration;

        public IntegrationTestConfiguration TestConfiguration
        {
            get
            {
                if (_testConfiguration == null)
                {
                    _testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration(_testSettingCategory);
                }

                return _testConfiguration;
            }
        }


        protected BaseApplication(string applicationName, string[] serviceNames, string testSettingCategory)
        {
            ApplicationName = applicationName;
            ServiceNames = serviceNames;
            _testSettingCategory = testSettingCategory;
        }

        public static string RootRepositoryPath { get; } = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\..\..\..\");

        public String MsbuildPath
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("MsBuildPath");
                if (path != null)
                {
                    return path;
                }

                if (File.Exists(@"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"))
                {
                    return @"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe";
                }

                if (File.Exists(@"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"))
                {
                    return @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe";
                }

                if (File.Exists(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\msbuild.exe"))
                {
                    return @"C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\msbuild.exe";
                }

                if (File.Exists(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MsBuild.exe"))
                {
                    return @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MsBuild.exe";
                }

                throw new Exception("Can not locate MsBuild.exe .");
            }
        }

        public virtual String[] NugetSources { get; } =
        {
            "https://api.nuget.org/v3/index.json"
        };

        public static string NugetPath { get; } = Path.GetFullPath(Path.Combine(RootRepositoryPath, @"build\Tools\nuget.4.4.1.exe"));

        public void InvokeAnExecutable(string executablePath, string arguments, string workingDirectory)
        {
            TestLogger?.WriteLine($@"[{DateTime.Now}] Executing: '{executablePath} {arguments}' in ({workingDirectory})");

            var startInfo = new ProcessStartInfo
            {
                Arguments = arguments,
                FileName = executablePath,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process process = Process.Start(startInfo);

            if (process == null)
            {
                throw new Exception($@"[{DateTime.Now}] {executablePath} process failed to start.");
            }

            LogProcessOutput(process.StandardOutput);
            LogProcessOutput(process.StandardError);

            process.WaitForExit();

            if (process.HasExited && process.ExitCode != 0)
            {
                throw new Exception("App server shutdown unexpectedly.");
            }

        }

        private void LogProcessOutput(TextReader reader)
        {
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                TestLogger?.WriteLine($@"[{DateTime.Now}] {line}");
            }
        }

        public abstract void InstallAgent();
        public abstract void BuildAndDeploy();
        public abstract void StopTestApplicationService();
    }
}
