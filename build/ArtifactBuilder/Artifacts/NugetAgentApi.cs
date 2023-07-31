using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAgentApi : Artifact
    {
        private readonly AgentComponents _frameworkAgentComponents;
        private readonly AgentComponents _coreAgentComponents;
        private string _nuGetPackageName;

        public NugetAgentApi(string configuration)
            : base(nameof(NugetAgentApi))
        {
            Configuration = configuration;
            ValidateContentAction = ValidateContent;

            _frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
            _coreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, "x64", RepoRootDirectory, HomeRootDirectory);
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            _frameworkAgentComponents.ValidateComponents();
            _coreAgentComponents.ValidateComponents();

            var package = new NugetPackage(StagingDirectory, OutputDirectory);
            package.CopyAll(PackageDirectory);
            package.CopyToLib(_frameworkAgentComponents.AgentApiDll, "net462");
            package.CopyToLib(_coreAgentComponents.AgentApiDll, "netstandard2.0");
            package.CopyToRoot(_frameworkAgentComponents.NewRelicLicenseFile);
            package.CopyToRoot(_frameworkAgentComponents.NewRelicThirdPartyNoticesFile);
            package.SetVersion(_frameworkAgentComponents.Version);
            _nuGetPackageName = package.Pack();
        }

        private void ValidateContent()
        {
            var unpackedLocation = Unpack();

            var expectedComponents = GetExpectedComponents(unpackedLocation);

            var unpackedComponents = GetUnpackedComponents(unpackedLocation);

            ValidationHelpers.ValidateComponents(expectedComponents, unpackedComponents, "Agent Api Nuget");

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

            // lib folder - api dlls
            var libFolder = Path.Combine(installedFilesRoot, "lib");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, Path.Combine(libFolder, "net462"), _frameworkAgentComponents.AgentApiDll);
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, Path.Combine(libFolder, "netstandard2.0"), _coreAgentComponents.AgentApiDll);

            // root folder
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents,installedFilesRoot, "LICENSE.txt");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, "THIRD_PARTY_NOTICES.txt");

            return expectedComponents;
        }

        private static SortedSet<string> GetUnpackedComponents(string installedFilesRoot)
        {
            var unpackedComponents = ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "lib"));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "images")));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "tools")));

            var ignoredRootFiles = new List<string> { "[Content_Types].xml", "NewRelic.Agent.Api.nuspec" };
            foreach (var file in Directory.EnumerateFiles(installedFilesRoot).Where(f => !IsIgnoredRootFile(f)))
            {
                unpackedComponents.Add(file);
            }

            return unpackedComponents;
        }

        private static bool IsIgnoredRootFile(string file)
        {
            return file.EndsWith("[Content_Types].xml") || file.EndsWith("NewRelic.Agent.Api.nuspec");
        }
    }
}
