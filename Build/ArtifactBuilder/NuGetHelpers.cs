using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ArtifactBuilder
{
	public static class NuGetHelpers
	{
		private const string _nugetPath = @"Tools\nuget.exe";

		public static void Pack(string nuspecFilePath, string outputDirectory)
		{
			var parameters = $@"Pack -NoPackageAnalysis {nuspecFilePath} -OutputDirectory {outputDirectory}";
			var process = Process.Start(_nugetPath, parameters);
			process.WaitForExit(30000);
			if (!process.HasExited)
			{
				process.Kill();
				throw new Exception($"Nuget pack failed complete in timely fashion.");
			}
			if (process.ExitCode != 0)
			{
				throw new Exception($"Nuget pack failed with exit code {process.ExitCode}.");
			}
		}

		public static void Push(NugetPushInfo nugetPushInfo, string outputDirectory)
		{
			var nupkgFile = Directory.EnumerateFiles(outputDirectory, "*.nupkg").FirstOrDefault();
			var parameters = $@"Push {nupkgFile} {nugetPushInfo.ApiKey} -Source {nugetPushInfo.ServerUri}";
			var process = System.Diagnostics.Process.Start(_nugetPath, parameters);
			process.WaitForExit(30000);
			if (!process.HasExited)
			{
				process.Kill();
				throw new Exception($"Nuget push failed complete in timely fashion.");
			}
			if (process.ExitCode != 0)
			{
				throw new Exception($"Nuget push failed with exit code {process.ExitCode}.");
			}
		}
	}
}