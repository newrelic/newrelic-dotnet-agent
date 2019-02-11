using System;
using System.Diagnostics;
using System.IO;
using Xunit.Abstractions;

namespace PlatformTests.Applications
{
	public abstract class BaseApplication
	{
		public string ApplicationName { get; }

		public string[] ServiceNames { get; }

		public ITestOutputHelper TestLogger { get; set; }

		protected BaseApplication(string applicationName, string[] serviceNames)
		{
			ApplicationName = applicationName;
			ServiceNames = serviceNames;
		}

		public static string RootRepositoryPath { get; } = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\..\..\");

		public String MsbuildPath
		{
			get
			{
				var path = Environment.GetEnvironmentVariable("MsBuildPath");
				if (path != null)
				{
					return path;
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
			"http://win-nuget-repository.pdx.vm.datanerd.us:81/NuGet/Default",
			"https://api.nuget.org/v3/index.json"
		};

		public static string NugetPath { get; } = Path.GetFullPath(Path.Combine(RootRepositoryPath, @"Build\Tools\nuget.exe"));

		public void InvokeAnExecutable(string executablePath, string arguments, string workingDirectory)
		{
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

		private async void LogProcessOutput(TextReader reader)
		{
			string line;

			while ((line = await reader.ReadLineAsync()) != null)
			{
				TestLogger?.WriteLine($@"[{DateTime.Now}] {line}");
			}
		}

		public abstract void InstallAgent();
		public abstract void BuildAndDeploy();
		public abstract void StopTestApplicationService();
	}
}
