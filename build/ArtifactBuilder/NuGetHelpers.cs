using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ArtifactBuilder
{
    public static class NuGetHelpers
    {
        private static readonly string _nugetPath = Path.Combine(FileHelpers.GetRepoRootDirectory(), @"Build\Tools\nuget.exe");

        public static string Pack(string nuspecFilePath, string outputDirectory)
        {
            var parameters = $@"Pack -NoPackageAnalysis ""{nuspecFilePath}"" -OutputDirectory ""{outputDirectory}""";
            var process = new Process();

            var startInfo = new ProcessStartInfo
            {
                FileName = _nugetPath,
                Arguments = parameters,
                RedirectStandardOutput = true
            };
            process.StartInfo = startInfo;

            process.Start();
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

            var stdOut = process.StandardOutput;
            var output = stdOut.ReadToEnd();
            Console.WriteLine(output);

            // output is expected to look like:
            // Attempting to build package from 'NewRelic.Azure.WebSites.x64.nuspec'.
            // Successfully created package 'C:\Source\Repos\newrelic-dotnet-agent\Build\BuildArtifacts\NugetAzureWebSites-x64\NewRelic.Azure.WebSites.x64.10.11.0.nupkg'.

            // capture the full path
            var regex = ".*Successfully created package '(.*)'.*";
            var matches = Regex.Match(output, regex);
            if (matches.Success && matches.Groups.Count > 1)
            {
                var packagePath = matches.Groups[1].Value;

                // verify the file exists, then return the filename
                if (File.Exists(packagePath))
                {
                    return Path.GetFileName(packagePath);
                }
            }

            throw new PackagingException("Failed to parse NuGet package filename.");
        }

        public static void Unpack(string nupkgFile, string outputDirectory)
        {
            FileHelpers.DeleteDirectories(outputDirectory);
            System.IO.Compression.ZipFile.ExtractToDirectory(nupkgFile, outputDirectory);
        }
    }
}
