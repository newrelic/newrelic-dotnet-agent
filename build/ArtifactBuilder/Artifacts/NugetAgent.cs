using System;
using System.Collections.Generic;
using System.IO;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAgent : Artifact
    {
        private readonly AgentComponents _frameworkAgentComponents;
        private readonly AgentComponents _frameworkAgentX86Components;
        private readonly AgentComponents _coreAgentComponents;
        private readonly AgentComponents _coreAgentArm64Components;
        private readonly AgentComponents _coreAgentX86Components;
        private string _nuGetPackageName;

        public NugetAgent(string configuration)
            : base(nameof(NugetAgent))
        {
            Configuration = configuration;
            ValidateContentAction = ValidateContent;

            _frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
            _frameworkAgentX86Components = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x86", RepoRootDirectory, HomeRootDirectory);
            _coreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
            _coreAgentArm64Components = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "arm64", RepoRootDirectory, HomeRootDirectory);
            _coreAgentX86Components = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "x86", RepoRootDirectory, HomeRootDirectory);
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            var rootDirectory = $@"{StagingDirectory}\content\newrelic";
            _frameworkAgentComponents.ValidateComponents();
            _frameworkAgentX86Components.ValidateComponents();
            _coreAgentComponents.ValidateComponents();
            _coreAgentArm64Components.ValidateComponents();
            _coreAgentX86Components.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);

            _frameworkAgentComponents.CopyComponents($@"{package.ContentDirectory}\newrelic");
            FileHelpers.CopyFile(_frameworkAgentX86Components.WindowsProfiler, $@"{package.ContentDirectory}\newrelic\x86");
            Directory.CreateDirectory($@"{rootDirectory}\logs");
            File.Create($@"{rootDirectory}\logs\placeholder").Dispose();

            _frameworkAgentComponents.CopyComponents($@"{package.GetContentFilesDirectory("any", "net462")}\newrelic");
            FileHelpers.CopyFile(_frameworkAgentX86Components.WindowsProfiler, $@"{package.GetContentFilesDirectory("any", "net462")}\newrelic\x86");
            Directory.CreateDirectory($@"{StagingDirectory}\contentFiles\any\net462\newrelic\logs");
            File.Create($@"{StagingDirectory}\contentFiles\any\net462\newrelic\logs\placeholder").Dispose();

            _coreAgentComponents.CopyComponents($@"{package.GetContentFilesDirectory("any", "netstandard2.0")}\newrelic");
            FileHelpers.CopyFile(_coreAgentX86Components.WindowsProfiler, $@"{package.GetContentFilesDirectory("any", "netstandard2.0")}\newrelic\x86");
            package.CopyToContentFiles(_coreAgentComponents.LinuxProfiler, @"any\netstandard2.0\newrelic");
            package.CopyToContentFiles(_coreAgentArm64Components.LinuxProfiler, @"any\netstandard2.0\newrelic\linux-arm64");
            Directory.CreateDirectory($@"{StagingDirectory}\contentFiles\any\netstandard2.0\newrelic\logs");
            File.Create($@"{StagingDirectory}\contentFiles\any\netstandard2.0\newrelic\logs\placeholder").Dispose();

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

            ValidationHelpers.ValidateComponents(expectedComponents, unpackedComponents, "Agent Nuget");

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

            // tools folder - Install scripts
            var toolsFolder = Path.Combine(installedFilesRoot, "tools");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, toolsFolder, "install.ps1");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, toolsFolder, "NewRelicHelper.psm1");

            // content folder - framework agent (x64 and x86)
            AddAllFrameworkAgentComponents(expectedComponents, Path.Combine(installedFilesRoot, "content", "newrelic"));

            // contentFiles folder - all agents
            var contentFilesRoot = Path.Combine(installedFilesRoot, "contentFiles", "any");
            AddAllFrameworkAgentComponents(expectedComponents, Path.Combine(contentFilesRoot, "net462", "newrelic"));
            AddAllCoreAgentComponents(expectedComponents, Path.Combine(contentFilesRoot, "netstandard2.0", "newrelic"));

            return expectedComponents;
        }

        private void AddAllFrameworkAgentComponents(SortedSet<string> expectedComponents, string folder)
        {
            AddFullAgentComponents(expectedComponents, folder, _frameworkAgentComponents);
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, Path.Combine(folder, "x86"), _frameworkAgentX86Components.WindowsProfiler);
        }

        private void AddAllCoreAgentComponents(SortedSet<string> expectedComponents, string folder)
        {
            AddFullAgentComponents(expectedComponents, folder, _coreAgentComponents);
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, Path.Combine(folder, "x86"), _coreAgentX86Components.WindowsProfiler);
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, Path.Combine(folder, "linux-arm64"), _coreAgentArm64Components.LinuxProfiler);
        }

        private static void AddFullAgentComponents(SortedSet<string> expectedComponents, string rootFolder, AgentComponents agentComponents)
        {
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, rootFolder, agentComponents.RootInstallDirectoryComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, rootFolder, agentComponents.AgentHomeDirComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, rootFolder, agentComponents.ConfigurationComponents);

            if (!string.IsNullOrEmpty(agentComponents.WindowsProfiler))
            {
                ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, rootFolder, agentComponents.WindowsProfiler);
            }
            if (!string.IsNullOrEmpty(agentComponents.LinuxProfiler))
            {
                ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, rootFolder, agentComponents.LinuxProfiler);
            }

            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, Path.Combine(rootFolder, "logs"), "placeholder");

            var extensionsFolder = Path.Combine(rootFolder, "extensions");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, extensionsFolder, agentComponents.WrapperXmlFiles);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, extensionsFolder, agentComponents.ExtensionDirectoryComponents);
        }

        private static SortedSet<string> GetUnpackedComponents(string installedFilesRoot)
        {
            var unpackedComponents = ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "content"));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "contentFiles")));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "images")));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "tools")));

            return unpackedComponents;
        }
    }
}
