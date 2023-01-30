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

        protected override string Unpack()
        {
            var unpackDir = $@"{OutputDirectory}\unpacked";
            FileHelpers.DeleteDirectories(unpackDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(_zipFilePath, unpackDir);
            return unpackDir;
        }

        protected override void ValidateContent()
        {
            var unpackDir = Unpack();

            //        var list = RootInstallDirectoryComponents
            //.Concat(AgentHomeDirComponents)
            //.Concat(ExtensionDirectoryComponents)
            //.Concat(WrapperXmlFiles)
            //.Append(ExtensionXsd)
            //.Append(AgentApiDll)
            //.ToList();

            //        if (!string.IsNullOrEmpty(LinuxProfiler))
            //        {
            //            list.Add(LinuxProfiler);
            //        }

            //        if (GRPCExtensionsLibLinux != null)
            //        {
            //            list.AddRange(GRPCExtensionsLibLinux);
            //        }

            //        return list;

            var allFrameworkComponents = _frameworkAgentComponents.AllComponents;
            var allFrameworkComponentsNormalized = new List<string>();
            var frameworkSourceHomeBuilderPath = $@"{_frameworkAgentComponents.HomeRootPath}\newrelichome_{_frameworkAgentComponents.Platform}";
            foreach (var component in allFrameworkComponents)
            {
                var normalized = component.Remove(0, frameworkSourceHomeBuilderPath.Length);
                allFrameworkComponentsNormalized.Add(normalized);
            }




            foreach (var component in allFrameworkComponentsNormalized)
            {
                var expectedComponent = Path.Join(unpackDir, component);

                if (!File.Exists(expectedComponent))
                {
                    throw new PackagingException($"Expected component {expectedComponent} is missing.");
                }
            }


            // cleanup
            FileHelpers.DeleteDirectories(unpackDir);
        }
    }
}
