using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            var unpackDir = $@"{OutputDirectory}\unpacked";
            FileHelpers.DeleteDirectories(unpackDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(_zipFilePath, unpackDir);
            return unpackDir;
        }

        private void ValidateContent()
        {
            var unpackDir = Unpack();

            // framework agent

            var frameworkSourceHomeBuilderPath = $@"{_frameworkAgentComponents.HomeRootPath}\newrelichome_{_frameworkAgentComponents.Platform}";

            var rootInstallDirComponents = RemoveSourceHomeBuilderPath(_frameworkAgentComponents.RootInstallDirectoryComponents, frameworkSourceHomeBuilderPath);
            var frameworkAgentHomeDirComponents = RemoveSourceHomeBuilderPath(_frameworkAgentComponents.AgentHomeDirComponents, frameworkSourceHomeBuilderPath);
            var frameworkExtensionDirComponents = RemoveSourceHomeBuilderPath(_frameworkAgentComponents.ExtensionDirectoryComponents, frameworkSourceHomeBuilderPath);
            var frameworkWrapperXmlFiles = RemoveSourceHomeBuilderPath(_frameworkAgentComponents.WrapperXmlFiles, frameworkSourceHomeBuilderPath);

            VerifyComponentsExist(rootInstallDirComponents, unpackDir, null);
            VerifyComponentsExist(frameworkAgentHomeDirComponents, unpackDir, FrameworkSubDirectoryName);
            VerifyComponentsExist(frameworkExtensionDirComponents, unpackDir, FrameworkSubDirectoryName);
            VerifyComponentsExist(frameworkWrapperXmlFiles, unpackDir, FrameworkSubDirectoryName);

            // core agent

            var coreSourceHomeBuilderPath = $@"{_coreAgentComponents.HomeRootPath}\newrelichome_{_coreAgentComponents.Platform}_coreclr";

            var rootCoreInstallDirComponents = RemoveSourceHomeBuilderPath(_coreAgentComponents.RootInstallDirectoryComponents, coreSourceHomeBuilderPath);
            var coreAgentHomeDirComponents = RemoveSourceHomeBuilderPath(_coreAgentComponents.AgentHomeDirComponents, coreSourceHomeBuilderPath);
            var coreExtensionDirComponents = RemoveSourceHomeBuilderPath(_coreAgentComponents.ExtensionDirectoryComponents, coreSourceHomeBuilderPath);
            var coreWrapperXmlFiles = RemoveSourceHomeBuilderPath(_coreAgentComponents.WrapperXmlFiles, coreSourceHomeBuilderPath);

            VerifyComponentsExist(rootCoreInstallDirComponents, unpackDir, null);
            VerifyComponentsExist(coreAgentHomeDirComponents, unpackDir, CoreSubDirectoryName);
            VerifyComponentsExist(coreExtensionDirComponents, unpackDir, CoreSubDirectoryName);
            VerifyComponentsExist(coreWrapperXmlFiles, unpackDir, CoreSubDirectoryName);

            // Open questions
            // 1. Why does the Agent API DLL get placed in the home dir for the core agent, but not the framework agent?
            // 2. Why do the RootInstallDirectoryComponents differ between framework and core?
            // 3. How to account for "agentInfo.json" in the home dir (both framework and core)?

            // cleanup
            FileHelpers.DeleteDirectories(unpackDir);
        }

        private List<string> RemoveSourceHomeBuilderPath(IReadOnlyCollection<string> components, string sourceHomeBuilderPath)
        {
            var returnList = new List<string>();
            foreach (var component in components)
            {
                returnList.Add(component.Remove(0, sourceHomeBuilderPath.Length));
            }
            return returnList;
        }

        private void VerifyComponentsExist(List<string> components, string root, string subdir)
        {
            foreach(var component in components)
            {
                var expectedComponent = Path.Join(root, subdir, component);
                if (!File.Exists(expectedComponent))
                {
                    throw new PackagingException($"Expected component {expectedComponent} is missing.");
                }
            }
        }
    }
}
