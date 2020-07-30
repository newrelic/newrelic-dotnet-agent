using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAzureWebSites : Artifact
    {
        public NugetAzureWebSites(string platform, string configuration)
            : base(nameof(NugetAzureWebSites) + "-" + platform)
        {
            Platform = platform;
            Configuration = configuration;
        }

        public string Configuration { get; }
        public string Platform { get; }
        private AgentComponents _agentComponents;

        private string RootDirectory => $@"{StagingDirectory}\content\newrelic";

        protected override void InternalBuild()
        {
            _agentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, Platform, RepoRootDirectory, HomeRootDirectory);
            _agentComponents.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);
            _agentComponents.CopyComponents($@"{package.ContentDirectory}\newrelic");
            package.CopyToLib(_agentComponents.AgentApiDll);
            package.CopyAll(PackageDirectory);
            TransformNewRelicConfig();
            package.SetVersion(_agentComponents.Version);
            package.Pack();
        }

        private void TransformNewRelicConfig()
        {
            var path = $@"{RootDirectory}\newrelic.config";
            var xml = new System.Xml.XmlDocument();

            // Update the 'newrelic.config' file
            xml.Load(path);
            var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
            ns.AddNamespace("x", "urn:newrelic-config");

            // Remove the 'application' element
            var node = xml.SelectSingleNode("//x:configuration/x:application", ns);
            node.ParentNode.RemoveChild(node);

            // Re-create the 'application' element
            var nodeLog = (System.Xml.XmlElement)xml.SelectSingleNode("//x:configuration/x:log", ns);
            var app = xml.CreateElement("application", "urn:newrelic-config");
            xml.DocumentElement.InsertBefore(app, nodeLog);

            // Set the 'directory' attribute
            nodeLog.SetAttribute("directory", @"c:\Home\LogFiles\NewRelic");
            xml.Save(path);
        }
    }
}
