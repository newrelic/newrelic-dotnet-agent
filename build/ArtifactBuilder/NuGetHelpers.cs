using System;
using System.Diagnostics;
using System.IO;

namespace ArtifactBuilder
{
    public static class NuGetHelpers
    {
        private static string _nugetPath = Path.Combine(FileHelpers.GetRepoRootDirectory(), @"Build\Tools\nuget.exe");

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
    }
}
