using System.Collections.Generic;
using System;
using System.IO;

namespace ArtifactBuilder.Artifacts
{
    public class AzureSiteExtension : Artifact
    {
        private const string XmlLibraryName = "Microsoft.Web.XmlTransform.dll";
        private const string NuGetLibraryName = "NuGet.Core.dll";
        private const string NuGetHelperLibraryName = "NewRelic.NuGetHelper.dll";

        private string _version;
        private string _nuGetPackageName;

        public AzureSiteExtension() : base(nameof(AzureSiteExtension))
        {
            ValidateContentAction = ValidateContent;
        }

        protected override void InternalBuild()
        {
            _version = ReadVersionFromFile();
            var package = new NugetPackage(StagingDirectory, OutputDirectory);
            package.CopyAll($@"{PackageDirectory}");
            package.CopyToContent($@"{RepoRootDirectory}\build\NewRelic.NuGetHelper\bin\{NuGetHelperLibraryName}");
            package.CopyToContent($@"{RepoRootDirectory}\build\NewRelic.NuGetHelper\bin\{NuGetLibraryName}");
            package.CopyToContent($@"{RepoRootDirectory}\build\NewRelic.NuGetHelper\bin\{XmlLibraryName}");
            package.SetVersion(_version);
            _nuGetPackageName = package.Pack();
        }

        private string ReadVersionFromFile()
        {
            var versionFile = $@"{RepoRootDirectory}\build\BuildArtifacts\_buildProperties\version_azuresiteextension.txt";

            if (!File.Exists(versionFile))
            {
                throw new PackagingException($"Version file does not exist: {versionFile}");
            }

            try
            {
                return File.ReadAllLines(versionFile)[0];
            }
            catch
            {
                throw new PackagingException($"Failed to read version file from: {versionFile}");
            }
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

            ValidationHelpers.ValidateComponents(expectedComponents, unpackedComponents, "Azure Site Extension");

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

            // content folder - installation items
            var contentFolder = Path.Combine(installedFilesRoot, "content");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, "applicationHost.xdt");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, "install.cmd");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, "install.ps1");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, XmlLibraryName);
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, NuGetLibraryName);
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, NuGetHelperLibraryName);
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, "uninstall.cmd");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, contentFolder, "web.config");

            return expectedComponents;
        }

        private static SortedSet<string> GetUnpackedComponents(string installedFilesRoot)
        {
            var unpackedComponents = ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "content"));
            unpackedComponents.UnionWith(ValidationHelpers.GetUnpackedComponents(Path.Combine(installedFilesRoot, "images")));

            return unpackedComponents;
        }
    }
}
