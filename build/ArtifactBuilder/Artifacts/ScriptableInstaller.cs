using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class ScriptableInstaller : Artifact
    {
        private readonly string FilesToZipFolderName;

        public ScriptableInstaller(string configuration)
            : base(nameof(ScriptableInstaller))
        {
            Configuration = configuration;

            FilesToZipFolderName = $@"{StagingDirectory}\FilesToZip";
        }

        public string Configuration { get; }

        protected override void InternalBuild()
        {
            var x64Components = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x64", SourceDirectory);
            var x86Components = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x86", SourceDirectory);
            x64Components.ValidateComponents();
            x86Components.ValidateComponents();

            FileHelpers.CopyAll($@"{PackageDirectory}\Installer", FilesToZipFolderName);
            var replacements = new Dictionary<string, string>() { { "AGENT_VERSION_STRING", x64Components.Version } };
            FileHelpers.ReplaceTextInFile($@"{FilesToZipFolderName}\install.ps1", replacements);

            var agentInfo = new AgentInfo
            {
                InstallType = "ScriptableFramework"
            };

            agentInfo.WriteToDisk(FilesToZipFolderName);

            CreateNugetPackage(x64Components, x86Components, $@"{PackageDirectory}\NewRelic.Net.Agent.x64.nuspec");
            CreateNugetPackage(x86Components, x86Components, $@"{PackageDirectory}\NewRelic.Net.Agent.nuspec");

            var zipFilePath = $@"{OutputDirectory}\NewRelic.Agent.Installer.{x64Components.Version}.zip";
            Directory.CreateDirectory(OutputDirectory);
            System.IO.Compression.ZipFile.CreateFromDirectory(FilesToZipFolderName, zipFilePath);
            File.WriteAllText($@"{OutputDirectory}\checksum.sha256", FileHelpers.GetSha256Checksum(zipFilePath));
        }

        private void CreateNugetPackage(AgentComponents components, AgentComponents x86Components, string nuspecPath)
        {
            var rootDir = $@"{StagingDirectory}\Nuget{components.Platform}";
            var stagingDir = $@"{rootDir}\content\newrelic";
            FileHelpers.CopyFile(nuspecPath, rootDir);

            var package = new NugetPackage(rootDir, FilesToZipFolderName);
            package.SetVersion(components.Version);
            var configFilePath = $@"{rootDir}\content\newrelic\newrelic.config";

            package.CopyToContent(components.RootInstallDirectoryComponents, @"newrelic");
            package.CopyToContent(components.RootInstallDirectoryComponents.Where(x => !x.Contains("newrelic.config") && !x.Contains("newrelic.xsd")), @"newrelic\ProgramFiles\NewRelic\NetAgent");
            package.CopyToContent(components.ExtensionDirectoryComponents.Where(x => x.Contains(".dll")), @"newrelic\ProgramFiles\NewRelic\NetAgent\Extensions");
            package.CopyToContent(x86Components.RootInstallDirectoryComponents.Where(x => x.Contains("NewRelic.Profiler.dll")), @"newrelic\ProgramFiles\NewRelic\NetAgent\x86");
            package.CopyToContent(components.WrapperXmlFiles, $@"newrelic\ProgramData\NewRelic\NetAgent\Extensions");
            package.CopyToContent(components.ExtensionXsd, $@"newrelic\ProgramData\NewRelic\NetAgent\Extensions");
            package.CopyToContent(components.NewRelicXsd, $@"newrelic\ProgramData\NewRelic\NetAgent");
            package.CopyToContent(configFilePath, $@"newrelic\ProgramData\NewRelic\NetAgent");

            //not sure why we create these folders
            Directory.CreateDirectory($@"{stagingDir}\Extensions");
            Directory.CreateDirectory($@"{stagingDir}\ProgramData\NewRelic\NetAgent\NewRelic\NetAgent\Extensions");
            Directory.CreateDirectory($@"{stagingDir}\ProgramData\NewRelic\NetAgent\NewRelic\NetAgent\Logs");

            File.Delete(configFilePath);
            package.Pack();
        }
    }
}
