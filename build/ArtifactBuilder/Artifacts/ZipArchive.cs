using System;
using System.Collections.Generic;
using System.IO;

namespace ArtifactBuilder.Artifacts
{

    public class ZipArchive : Artifact
    {
        private const string FrameworkSubDirectoryName = "netframework";
        private const string CoreSubDirectoryName = "netcore";

        private AgentComponents _frameworkAgentComponents;
        private AgentComponents _coreAgentComponents;
        private string _zipFilePath;

        public ZipArchive(string platform, string configuration) : base(nameof(ZipArchive))
        {
            Platform = platform;
            Configuration = configuration;
            StagingDirectory = $@"{RepoRootDirectory}\build\_staging\{Name}-{Platform}";
            OutputDirectory = $@"{RepoRootDirectory}\build\BuildArtifacts\{Name}-{Platform}";
            ValidateContentAction = ValidateContent;

            _frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, Platform, RepoRootDirectory, HomeRootDirectory);
            _coreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, Platform, RepoRootDirectory, HomeRootDirectory);
            _zipFilePath = $@"{OutputDirectory}\NewRelicDotNetAgent_{_frameworkAgentComponents.SemanticVersion}_{Platform}.zip";
        }

        public string Configuration { get; }
        public string Platform { get; }

        protected override void InternalBuild()
        {
            _frameworkAgentComponents.ValidateComponents();
            _frameworkAgentComponents.CopyComponents(StagingDirectory, FrameworkSubDirectoryName);

            _coreAgentComponents.ValidateComponents();
            _coreAgentComponents.CopyComponents(StagingDirectory, CoreSubDirectoryName);

            var agentInfo = new AgentInfo
            {
                InstallType = $"ZipWin{Platform}"
            };

            agentInfo.WriteToDisk(Path.Combine(StagingDirectory, FrameworkSubDirectoryName));
            agentInfo.WriteToDisk(Path.Combine(StagingDirectory, CoreSubDirectoryName));

            Directory.CreateDirectory(OutputDirectory);
            System.IO.Compression.ZipFile.CreateFromDirectory(StagingDirectory, _zipFilePath);
            File.WriteAllText($@"{OutputDirectory}\checksum.sha256", FileHelpers.GetSha256Checksum(_zipFilePath));

            Console.WriteLine($"Successfully created artifact for {nameof(ZipArchive)}.");
        }

        private string Unpack()
        {
            var unpackedDir = Path.Join(OutputDirectory, "unpacked");
            if (Directory.Exists(unpackedDir))
            {
                FileHelpers.DeleteDirectories(unpackedDir);
            }
            System.IO.Compression.ZipFile.ExtractToDirectory(_zipFilePath, unpackedDir);
            return unpackedDir;
        }

        private void ValidateContent()
        {
            var unpackedLocation = Unpack();

            var installedFilesRoot = unpackedLocation;

            var expectedComponents = GetExpectedComponents(installedFilesRoot);

            var unpackedComponents = ValidationHelpers.GetUnpackedComponents(installedFilesRoot);

            var missingExpectedComponents = new SortedSet<string>(expectedComponents, StringComparer.OrdinalIgnoreCase);
            missingExpectedComponents.ExceptWith(unpackedComponents);
            foreach (var missingComponent in missingExpectedComponents)
            {
                throw new PackagingException($"The unpacked ZIP archive was missing the expected component {missingComponent}");
            }

            var unexpectedUnpackedComponents = new SortedSet<string>(unpackedComponents, StringComparer.OrdinalIgnoreCase);
            unexpectedUnpackedComponents.ExceptWith(expectedComponents);
            foreach (var unexpectedComponent in unexpectedUnpackedComponents)
            {
                throw new PackagingException($"The unpacked ZIP archive contained an unexpected component {unexpectedComponent}");
            }

            // cleanup
            FileHelpers.DeleteDirectories(unpackedLocation);
        }

        private SortedSet<string> GetExpectedComponents(string installedFilesRoot)
        {
            var expectedComponents = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // framework agent

            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _frameworkAgentComponents.RootInstallDirectoryComponents);

            var netframeworkFolder = Path.Join(installedFilesRoot, FrameworkSubDirectoryName);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netframeworkFolder, _frameworkAgentComponents.AgentHomeDirComponents);
            expectedComponents.Add(Path.Join(netframeworkFolder, AgentInfo.AgentInfoFilename));

            var netframeworkExtensionsFolder = Path.Join(netframeworkFolder, "extensions");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netframeworkExtensionsFolder, _frameworkAgentComponents.ExtensionDirectoryComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netframeworkExtensionsFolder, _frameworkAgentComponents.WrapperXmlFiles);

            // core agent

            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _coreAgentComponents.RootInstallDirectoryComponents);

            var netcoreFolder = Path.Join(installedFilesRoot, CoreSubDirectoryName);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netcoreFolder, _coreAgentComponents.AgentHomeDirComponents);
            expectedComponents.Add(Path.Join(netcoreFolder, AgentInfo.AgentInfoFilename));

            var netcoreExtensionsFolder = Path.Join(netcoreFolder, "extensions");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netcoreExtensionsFolder, _coreAgentComponents.ExtensionDirectoryComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netcoreExtensionsFolder, _coreAgentComponents.WrapperXmlFiles);

            return expectedComponents;
        }

    }
}
