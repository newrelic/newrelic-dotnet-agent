using System;
using System.Collections.Generic;
using System.IO;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAzureCloudServices : Artifact
    {
        private readonly AgentComponents _frameworkAgentComponents;
        private string _nuGetPackageName;

        public NugetAzureCloudServices(string configuration)
            : base(nameof(NugetAzureCloudServices))
        {
            Configuration = configuration;
            ValidateContentAction = ValidateContent;

            _frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            _frameworkAgentComponents.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);
            package.CopyAll(PackageDirectory);
            DoInstallerReplacements($"NewRelicAgent_x64_{_frameworkAgentComponents.Version}.msi");
            package.CopyToLib(_frameworkAgentComponents.AgentApiDll);
            package.CopyToContent($@"{RepoRootDirectory}\src\_build\x64-{Configuration}\Installer\{GetMsiName()}");
            package.SetVersion(_frameworkAgentComponents.Version);
            _nuGetPackageName = package.Pack();
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

        private string GetMsiName()
        {
            return $"NewRelicAgent_x64_{_frameworkAgentComponents.Version}.msi";
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

            ValidationHelpers.ValidateComponents(expectedComponents, unpackedComponents, "Azure Cloud Services Nuget");

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
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, toolsFolder, "uninstall.ps1");

            // lib folder - api dll
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, Path.Combine(installedFilesRoot, "lib"), _frameworkAgentComponents.AgentApiDll);

            // content folder - installer
            var contentFolder = Path.Combine(installedFilesRoot, "content");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, "newrelic.cmd");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, GetMsiName());

            return expectedComponents;
        }

        private static SortedSet<string> GetUnpackedComponents(string installedFilesRoot)
        {
            var unpackedComponents = ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "content"));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "lib")));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "images")));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "tools")));

            return unpackedComponents;
        }
    }
}
