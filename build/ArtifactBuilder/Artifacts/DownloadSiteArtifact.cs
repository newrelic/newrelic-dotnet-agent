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
            Version = agentComponents.SemanticVersion;
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
                    $@"NewRelicDotNetAgent_{Version}_{platform}.msi");
            }

            //Zip files
            foreach (var platform in platforms)
            {
                CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\ZipArchive-{platform}", "*.zip", OutputDirectory,
                    $@"NewRelicDotNetAgent_{Version}_{platform}.zip");
            }

            //Linux packages
            CopyAllFilesAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\LinuxDeb", "*.deb", OutputDirectory);
            CopyAllFilesAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\LinuxRpm", "*.rpm", OutputDirectory);
            CopyAllFilesAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\LinuxTar", "*.tar.gz", OutputDirectory);

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

        private void CopyAllFilesAndChecksum(string sourceDirectory, string sourceFileSearchPattern, string destinationDirectory)
        {
            var files = Directory.GetFiles(sourceDirectory, sourceFileSearchPattern);

            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);

                File.Copy(filePath, $@"{destinationDirectory}\{fileName}");
                File.Copy($"{filePath}.sha256", $@"{ShaDirectory}\{fileName}.sha256");
            }
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
