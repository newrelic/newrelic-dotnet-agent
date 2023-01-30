using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAgent : Artifact
    {
        public NugetAgent(string configuration)
            : base(nameof(NugetAgent))
        {
            Configuration = configuration;
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            var rootDirectory = $@"{StagingDirectory}\content\newrelic";
            var frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
            var frameworkAgentX86Components = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x86", RepoRootDirectory, HomeRootDirectory);
            var coreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
            var coreAgentArm64Components = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "arm64", RepoRootDirectory, HomeRootDirectory);
            var coreAgentX86Components = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "x86", RepoRootDirectory, HomeRootDirectory);
            frameworkAgentComponents.ValidateComponents();
            frameworkAgentX86Components.ValidateComponents();
            coreAgentComponents.ValidateComponents();
            coreAgentArm64Components.ValidateComponents();
            coreAgentX86Components.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);

            frameworkAgentComponents.CopyComponents($@"{package.ContentDirectory}\newrelic");
            FileHelpers.CopyFile(frameworkAgentX86Components.WindowsProfiler, $@"{package.ContentDirectory}\newrelic\x86");
            Directory.CreateDirectory($@"{rootDirectory}\logs");
            System.IO.File.Create($@"{rootDirectory}\logs\placeholder").Dispose();

            frameworkAgentComponents.CopyComponents($@"{package.GetContentFilesDirectory("any", "net462")}\newrelic");
            FileHelpers.CopyFile(frameworkAgentX86Components.WindowsProfiler, $@"{package.GetContentFilesDirectory("any", "net462")}\newrelic\x86");
            Directory.CreateDirectory($@"{StagingDirectory}\contentFiles\any\net462\newrelic\logs");
            System.IO.File.Create($@"{StagingDirectory}\contentFiles\any\net462\newrelic\logs\placeholder").Dispose();

            coreAgentComponents.CopyComponents($@"{package.GetContentFilesDirectory("any", "netstandard2.0")}\newrelic");
            FileHelpers.CopyFile(coreAgentX86Components.WindowsProfiler, $@"{package.GetContentFilesDirectory("any", "netstandard2.0")}\newrelic\x86");
            package.CopyToContentFiles(coreAgentComponents.LinuxProfiler, @"any\netstandard2.0\newrelic");
            package.CopyToContentFiles(coreAgentArm64Components.LinuxProfiler, @"any\netstandard2.0\newrelic\linux-arm64");
            package.CopyToContentFiles(coreAgentComponents.GRPCExtensionsLibLinux, @"any\netstandard2.0\newrelic");
            package.CopyToContentFiles(coreAgentArm64Components.GRPCExtensionsLibLinux, @"any\netstandard2.0\newrelic");
            Directory.CreateDirectory($@"{StagingDirectory}\contentFiles\any\netstandard2.0\newrelic\logs");
            System.IO.File.Create($@"{StagingDirectory}\contentFiles\any\netstandard2.0\newrelic\logs\placeholder").Dispose();

            package.CopyAll(PackageDirectory);
            var agentInfo = new AgentInfo
            {
                InstallType = "NugetAgent"
            };

            var newRelicConfigPaths = new[]
            {
                $@"{rootDirectory}\newrelic.config",
                $@"{StagingDirectory}\contentFiles\any\net462\newrelic\newrelic.config",
                $@"{StagingDirectory}\contentFiles\any\netstandard2.0\newrelic\newrelic.config",
            };

            foreach (var newRelicConfigPath in newRelicConfigPaths)
            {
                TransformNewRelicConfig(newRelicConfigPath);
                agentInfo.WriteToDisk(Path.GetDirectoryName(newRelicConfigPath));
            }

            package.SetVersion(frameworkAgentComponents.Version);

            package.Pack();
        }

        protected override string Unpack()
        {
            throw new NotImplementedException();
        }

        protected override void ValidateContent()
        {
            throw new NotImplementedException();
        }

        private void TransformNewRelicConfig(string newRelicConfigPath)
        {
            var xml = new System.Xml.XmlDocument();

            // Update the 'newrelic.config' file
            xml.Load(newRelicConfigPath);
            var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
            ns.AddNamespace("x", "urn:newrelic-config");

            // Remove the 'application' element
            var node = xml.SelectSingleNode("//x:configuration/x:application", ns);
            node.ParentNode.RemoveChild(node);

            // Re-create the 'application' element
            var nodeLog = (System.Xml.XmlElement)xml.SelectSingleNode("//x:configuration/x:log", ns);
            var app = xml.CreateElement("application", "urn:newrelic-config");
            xml.DocumentElement.InsertBefore(app, nodeLog);

            xml.Save(newRelicConfigPath);
        }
    }
}
