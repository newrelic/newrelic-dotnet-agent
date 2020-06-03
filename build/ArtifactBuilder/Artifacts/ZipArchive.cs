using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{

    public class ZipArchive : Artifact
    {
        public ZipArchive(AgentType agentType, string platform, string configuration) : base(nameof(ZipArchive) + agentType)
        {
            AgentType = agentType;
            Platform = platform;
            Configuration = configuration;
            StagingDirectory = $@"{SourceDirectory}\Build\_staging\{Name}-{Platform}";
            OutputDirectory = $@"{SourceDirectory}\Build\BuildArtifacts\{Name}-{Platform}";
        }

        public AgentType AgentType { get; }
        public string Configuration { get; }
        public string Platform { get; }

        protected override void InternalBuild()
        {
            var agentComponents = AgentComponents.GetAgentComponents(AgentType, Configuration, Platform, SourceDirectory);
            agentComponents.ValidateComponents();
            agentComponents.CopyComponents(StagingDirectory);

            var agentInfo = new AgentInfo
            {
                InstallType = $"ZipWin{Platform}{AgentType.ToString()}"
            };

            agentInfo.WriteToDisk(StagingDirectory);

            var zipFilePath = AgentType == AgentType.Framework
                ? $@"{OutputDirectory}\newrelic-framework-agent_{agentComponents.Version}_{Platform}.zip"
                : $@"{OutputDirectory}\newrelic-netcore20-agent-win_{agentComponents.Version}_{Platform}.zip";
            Directory.CreateDirectory(OutputDirectory);
            System.IO.Compression.ZipFile.CreateFromDirectory(StagingDirectory, zipFilePath);
            File.WriteAllText($@"{OutputDirectory}\checksum.sha256", FileHelpers.GetSha256Checksum(zipFilePath));

            // For now, the DotNet-Core20-Agent-DeployToS3 job expects core agent artifacts to be in the following directory
            // At some point we should change the job to pull from the new location under the Build\BuildArtifacts directory
            if (AgentType == AgentType.Core)
            {
                FileHelpers.CopyFile(zipFilePath, $@"{SourceDirectory}\Agent\_build\CoreArtifacts");
            }
        }
    }
}
