using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class CoreInstaller : Artifact
    {

        public CoreInstaller(string configuration) : base("ZipArchiveCoreInstaller")
        {
            Configuration = configuration;
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            var x64Components = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "x64", SourceDirectory);
            var x86Components = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "x86", SourceDirectory);
            x64Components.ValidateComponents();
            x86Components.ValidateComponents();

            FileHelpers.DeleteDirectories(StagingDirectory, OutputDirectory);
            var agentInfox64 = new AgentInfo
            {
                InstallType = "ScriptableCore"
            };
            x64Components.CopyComponents($@"{StagingDirectory}\x64");
            x86Components.CopyComponents($@"{StagingDirectory}\x86");
            agentInfox64.WriteToDisk($@"{StagingDirectory}\x64");
            agentInfox64.WriteToDisk($@"{StagingDirectory}\x86");

            FileHelpers.CopyFile($@"{SourceDirectory}\Build\Packaging\CoreInstaller\installAgent.ps1", StagingDirectory);
            FileHelpers.CopyFile($@"{SourceDirectory}\Build\Packaging\CoreInstaller\installAgentUsage.txt", StagingDirectory);

            var zipFilePath = $@"{OutputDirectory}\newrelic-netcore20-agent-win-installer_{x64Components.Version}.zip";
            Directory.CreateDirectory(OutputDirectory);
            System.IO.Compression.ZipFile.CreateFromDirectory(StagingDirectory, zipFilePath);
            File.WriteAllText($@"{OutputDirectory}\checksum.sha256", FileHelpers.GetSha256Checksum(zipFilePath));

            // For now, the DotNet-Core20-Agent-DeployToS3 job expects core agent artifacts to be in the following directory
            // At some point we should change the job to pull from the new location under the Build\BuildArtifacts directory
            FileHelpers.CopyFile(zipFilePath, $@"{SourceDirectory}\Agent\_build\CoreArtifacts");

            // We put a readme file for the core agent on the download site: http://download.newrelic.com/dot_net_agent/core_20/current/
            // This readme also gets picked up by the DotNet-Core20-Agent-DeployToS3 job.
            CopyCoreReadme();
        }

        private void CopyCoreReadme()
        {
            var readmeFileName = "netcore20-agent-readme.md";
            var srcReadmeFile = Path.Combine(SourceDirectory, "src", "Agent", "Miscellaneous", readmeFileName);
            var dstReadmeFilePath = Path.Combine(SourceDirectory, "Agent", "_build", "CoreArtifacts");
            var dstReadmeFile = Path.Combine(dstReadmeFilePath, readmeFileName);
            FileHelpers.CopyFile(srcReadmeFile, dstReadmeFilePath);
            var renamedReadmeFile = dstReadmeFile.Replace(readmeFileName, "README.md");
            if (File.Exists(renamedReadmeFile)) File.Delete(renamedReadmeFile);
            File.Move(dstReadmeFile, renamedReadmeFile);
        }
    }
}
