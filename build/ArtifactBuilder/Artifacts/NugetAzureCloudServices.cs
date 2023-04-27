using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAzureCloudServices : Artifact
    {
        public NugetAzureCloudServices(string configuration)
            : base(nameof(NugetAzureCloudServices))
        {
            Configuration = configuration;
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            var frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
            frameworkAgentComponents.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);
            package.CopyAll(PackageDirectory);
            DoInstallerReplacements($"NewRelicAgent_x64_{frameworkAgentComponents.Version}.msi");
            package.CopyToLib(frameworkAgentComponents.AgentApiDll);
            package.CopyToContent($@"{RepoRootDirectory}\src\_build\x64-{Configuration}\Installer\NewRelicAgent_x64_{frameworkAgentComponents.Version}.msi");
            package.SetVersion(frameworkAgentComponents.Version);
            package.Pack();
        }

        private void DoInstallerReplacements(string agentInstaller)
        {
            var paths = new[] {
                $@"{StagingDirectory}\content\newrelic.cmd",
                $@"{StagingDirectory}\tools\install.ps1"
            };

            foreach (var path in paths)
            {
                var contents = File.ReadAllText(path);
                contents = contents
                    .Replace("AGENT_INSTALLER", agentInstaller);
                if (!contents.Contains(agentInstaller))
                {
                    throw new Exception($"Unable to set version in {path}");
                }
                File.WriteAllText(path, contents);
            }
        }
    }
}
