using System.Collections.Generic;
using System;
using System.IO;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAzureWebSites : Artifact
    {
        public NugetAzureWebSites(string platform, string configuration)
            : base(nameof(NugetAzureWebSites) + "-" + platform)
        {
            Platform = platform;
            Configuration = configuration;
            ValidateContentAction = ValidateContent;

            _frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, Platform, RepoRootDirectory, HomeRootDirectory);
        }

        public string Configuration { get; }
        public string Platform { get; }
        private readonly AgentComponents _frameworkAgentComponents;
        private string _nuGetPackageName;

        protected override void InternalBuild()
        {
            var rootDirectory = $@"{StagingDirectory}\content\newrelic";
            _frameworkAgentComponents.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);

            _frameworkAgentComponents.CopyComponents($@"{package.ContentDirectory}\newrelic");
            _frameworkAgentComponents.CopyComponents($@"{package.GetContentFilesDirectory("any", "net462")}\newrelic");
            package.CopyToLib(_frameworkAgentComponents.AgentApiDll);

            package.CopyAll(PackageDirectory);
            var agentInfo = new AgentInfo
            {
                InstallType = $"NugetAzureWebsites{Platform}"
            };

            var newRelicConfigPaths = new[]
            {
                $@"{rootDirectory}\newrelic.config",
                $@"{StagingDirectory}\contentFiles\any\net462\newrelic\newrelic.config",
            };

            foreach (var newRelicConfigPath in newRelicConfigPaths)
            {
                TransformNewRelicConfig(newRelicConfigPath);
                agentInfo.WriteToDisk(Path.GetDirectoryName(newRelicConfigPath));
            }

            package.SetVersion(_frameworkAgentComponents.Version);

            _nuGetPackageName = package.Pack();
        }

        private static void TransformNewRelicConfig(string newRelicConfigPath)
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

            // Set the 'directory' attribute
            nodeLog.SetAttribute("directory", @"c:\Home\LogFiles\NewRelic");
            xml.Save(newRelicConfigPath);
        }

        /// <summary>
        /// This method will not validate the contents of every directory in the unpacked nuget.
        /// The validation will focus on the components that we expect to be included in the nuget
        /// which aligns with what we expect to be defined in the nuspec file.
        /// </summary>
        private void ValidateContent()
        {
            var unpackedLocation = Unpack();

            var expectedComponents = GetExpectedComponents(unpackedLocation);

            var unpackedComponents = GetUnpackedComponents(unpackedLocation);

            ValidationHelpers.ValidateComponents(expectedComponents, unpackedComponents, $"Azure WebSites {Platform} Nuget");

            FileHelpers.DeleteDirectories(unpackedLocation);
        }

        private string Unpack()
        {
            if (string.IsNullOrEmpty(_nuGetPackageName))
                throw new PackagingException("NuGet package name not found. Did you call InternalBuild()?");

            var unpackDir = Path.Join(OutputDirectory, "unpacked");

            var nugetFile = Path.Join(OutputDirectory, _nuGetPackageName);

            NuGetHelpers.Unpack(nugetFile, unpackDir);
            return unpackDir;
        }

        private SortedSet<string> GetExpectedComponents(string installedFilesRoot)
        {
            var expectedComponents = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // images folder - New Relic icon
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, Path.Combine(installedFilesRoot, "images"), "icon.png");

            // README
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, "README.md");

            // tools folder - Install scripts
            var toolsFolder = Path.Combine(installedFilesRoot, "tools");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, toolsFolder, "install.ps1");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, toolsFolder, "uninstall.ps1");

            // content folder - framework agent
            AddFullAgentComponents(expectedComponents, Path.Combine(installedFilesRoot, "content", "newrelic"));

            // contentFiles folder - all agents
            var contentFilesRoot = Path.Combine(installedFilesRoot, "contentFiles", "any");
            AddFullAgentComponents(expectedComponents, Path.Combine(contentFilesRoot, "net462", "newrelic"));

            // lib folder - agent api
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, Path.Combine(installedFilesRoot, "lib"), _frameworkAgentComponents.AgentApiDll);

            return expectedComponents;
        }

        private void AddFullAgentComponents(SortedSet<string> expectedComponents, string rootFolder)
        {
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, rootFolder, _frameworkAgentComponents.RootInstallDirectoryComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, rootFolder, _frameworkAgentComponents.AgentHomeDirComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, rootFolder, _frameworkAgentComponents.ConfigurationComponents);

            var extensionsFolder = Path.Combine(rootFolder, "extensions");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, extensionsFolder, _frameworkAgentComponents.WrapperXmlFiles);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, extensionsFolder, _frameworkAgentComponents.ExtensionDirectoryComponents);
        }

        private static SortedSet<string> GetUnpackedComponents(string installedFilesRoot)
        {
            var unpackedComponents = ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "content"));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "contentFiles")));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "lib")));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "images")));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "tools")));
            unpackedComponents.Add(Path.Combine(installedFilesRoot, "README.md"));

            return unpackedComponents;
        }
    }
}
