using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAzureCloudServices : Artifact
    {
        public NugetAzureCloudServices(string configuration, string sourceDirectory)
            : base(sourceDirectory, nameof(NugetAzureCloudServices))
        {
            Configuration = configuration;
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            var frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x64", SourceDirectory);
            frameworkAgentComponents.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);
            package.CopyAll(PackageDirectory);
            var serverMonitorFileName = DownloadServerMonitorMsi();
            DoInstallerReplacements($"NewRelicAgent_x64_{frameworkAgentComponents.Version}.msi", serverMonitorFileName);
            package.CopyToLib(frameworkAgentComponents.AgentApiDll);
            package.CopyToContent($@"{SourceDirectory}\src\_build\x64-{Configuration}\Installer\NewRelicAgent_x64_{frameworkAgentComponents.Version}.msi");
            package.SetVersion(frameworkAgentComponents.Version);
            package.Pack();
        }

        private string DownloadServerMonitorMsi()
        {
            using (var client = new WebClient())
            {
                var xml = client.DownloadString("https://nr-downloads-main.s3.amazonaws.com/?delimiter=/&prefix=windows_server_monitor/release/");

                var xdoc = XDocument.Parse(xml);
                var ns = xdoc.Root.GetDefaultNamespace();
                var items = xdoc.Root.Elements(ns + "Contents")
                    .Select(x => x.Element(ns + "Key"))
                    .Where(x => x.Value.Contains("x64") && x.Value.Contains("msi"))
                    .Select(x => x.Value)
                    .ToList();
                items.Sort();

                var filePath = items.Last();
                var fileName = filePath.Replace("windows_server_monitor/release/", string.Empty);

                var bytes = client.DownloadData($"https://download.newrelic.com/{filePath}");

                var path = $@"{StagingDirectory}\content\{fileName}";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);

                return fileName;
            }
        }

        private void DoInstallerReplacements(string agentInstaller, string serverMonitorInstaller)
        {
            var paths = new[] {
                $@"{StagingDirectory}\content\newrelic.cmd",
                $@"{StagingDirectory}\tools\install.ps1"
            };

            foreach (var path in paths)
            {
                var contents = File.ReadAllText(path);
                contents = contents
                    .Replace("AGENT_INSTALLER", agentInstaller)
                    .Replace("SERVERMONITOR_INSTALLER", serverMonitorInstaller);
                if (!contents.Contains(agentInstaller) || !contents.Contains(serverMonitorInstaller))
                {
                    throw new Exception($"Unable to set version in {path}");
                }
                File.WriteAllText(path, contents);
            }
        }
    }
}
