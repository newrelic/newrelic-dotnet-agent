using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class DownloadSiteArtifact : Artifact
    {
        private const string SourceShaFileName = "checksum.sha256";

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

            //Framework Zip files
            foreach (var platform in platforms)
            {
                CopyFileAndChecksum($@"{RepoRootDirectory}\build\BuildArtifacts\ZipArchiveFramework-{platform}", "*.zip", OutputDirectory, $@"newrelic-agent-win-{platform}-{Version}.zip");
            }

            //Copying Readme.txt file
            FileHelpers.CopyFile($@"{PackageDirectory}\Readme.txt", $@"{OutputDirectory}");
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
            File.Copy($@"{sourceDirectory}\{SourceShaFileName}", $@"{ShaDirectory}\{destinationFileName}.sha256");
        }
    }
}
