using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class DownloadSiteArtifact : Artifact
    {
        private const string ShaFileExtension = ".sha256";
        private const string SourceShaFileName = "checksum" + ShaFileExtension;
        private const string ShaMarkdownTableFileName = "checksums.md";

        public string Version { get; }
        public string ShaDirectory { get; }

        public DownloadSiteArtifact(string configuration) : base("DownloadSite")
        {
            OutputDirectory = $@"{RepoRootDirectory}\build\BuildArtifacts\{Name}";
            ShaDirectory = OutputDirectory + @"\SHA256";
            var agentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, configuration, "x64", RepoRootDirectory, HomeRootDirectory);
            Version = agentComponents.Version;
        }

        protected override void InternalBuild()
        {
            Directory.CreateDirectory(OutputDirectory);
            Directory.CreateDirectory(ShaDirectory);

            List<string> platforms = new List<string>()
            {
                "x86",
                "x64"
            };

            //Msi Installer
            foreach (var platform in platforms)
            {
                CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\MsiInstaller-{platform}", "*.msi", OutputDirectory,
                    $@"newrelic-agent-win-{platform}-{Version}.msi");
            }

            //Scriptable Installer
            CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\ScriptableInstaller", "*.zip", OutputDirectory, $@"newrelic-agent-win-{Version}-scriptable-installer.zip");

            //Core Scriptable Installer
            CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\ZipArchiveCoreInstaller", "*.zip", OutputDirectory, $@"newrelic-netcore20-agent-win-{Version}-scriptable-installer.zip");

            //Core Zip files
            foreach (var platform in platforms)
            {
                CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\ZipArchiveCore-{platform}", "*.zip", OutputDirectory, $@"newrelic-netcore20-agent-win-{platform}-{Version}.zip");
            }

            //Framework Zip files
            foreach (var platform in platforms)
            {
                CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\ZipArchiveFramework-{platform}", "*.zip", OutputDirectory, $@"newrelic-agent-win-{platform}-{Version}.zip");
            }

            //Linux packages
            CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\LinuxDeb", "*.deb", OutputDirectory);
            CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\LinuxRpm", "*.rpm", OutputDirectory);
            CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\LinuxTar", "*.tar.gz", OutputDirectory);

            //Copying Readme.txt file
            FileHelpers.CopyFile($@"{PackageDirectory}\Readme.txt", $@"{OutputDirectory}");

            //Create markdown file with table of SHA values for the release notes
            CreateSHAValuesTableMarkdownFile(ShaDirectory, ShaMarkdownTableFileName);
        }

        private void CopyFileAndChecksum(string sourceDirectory, string sourceFileSearchPattern, string destinationDirectory, string destinationFileName = null)
        {
            var filePath = Directory.GetFiles(sourceDirectory, sourceFileSearchPattern).First();

            if (destinationFileName == null)
            {
                var fileInfo = new FileInfo(filePath);
                destinationFileName = fileInfo.Name;
            }

            File.Copy(filePath, $@"{destinationDirectory}\{destinationFileName}");
            File.Copy($@"{sourceDirectory}\{SourceShaFileName}", $@"{ShaDirectory}\{destinationFileName}{ShaFileExtension}");
        }

        private void CreateSHAValuesTableMarkdownFile(string shaDirectory, string outputFilename)
        {
            var outputLines = new List<string>() { { "### Checksums" }, { "| File | SHA - 256  Hash |" }, { "| ---| ---|" } };

            // Add filename and sha value for each .sha256 file found
            var shaFilenames = Directory.GetFiles(shaDirectory, $"*{ShaFileExtension}");
            foreach (var shaFilename in shaFilenames)
            {
                var baseFilename = Path.GetFileNameWithoutExtension(shaFilename);
                var contents = File.ReadAllText(shaFilename);
                var shaValue = contents.Split(" ")[0]; // this handles the Linux files which have both the SHA value and the filename in them
                outputLines.Add($"| {baseFilename} | {shaValue} |");
            }

            File.WriteAllLines(Path.Combine(shaDirectory, outputFilename), outputLines.ToArray());
        }
    }
}
