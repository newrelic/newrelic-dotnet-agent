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
		private const string _proGetUri = @"http://win-nuget-repository.pdx.vm.datanerd.us:81/nuget/Default";
		private const string _proGetApiKey = @"C7B30E3332814310896ADB3DEC35F491";

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

		public static void PushToProGet(string outputDirectory)
		{
			var nupkgFile = Directory.EnumerateFiles(outputDirectory, "*.nupkg").FirstOrDefault();
			var parameters = $@"Push {nupkgFile} {_proGetApiKey} -Source {_proGetUri}";
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